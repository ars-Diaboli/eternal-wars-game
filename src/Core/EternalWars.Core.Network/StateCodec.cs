using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Network;

/// <summary>
/// Writes pieces of a running game (a hero, a map object, a battle, a decision, an event) as JSON and reads them back into new objects, field by
/// field, for a LAN client's view (ADR-193). It needs no per-type code: whatever the rules keep in a hero or a town crosses the wire, and a type
/// that gains a field is carried with it.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Every instance field is written, private ones and those of base types included; delegates are not.</item>
/// <item>Creature and map object definitions are written by id and read from the reading game's catalogs, when those hold the same id.</item>
/// <item>Lists, sets and dictionaries are written as their items; a field typed as an interface reads back a <see cref="List{T}"/>,
/// <see cref="HashSet{T}"/> or <see cref="Dictionary{TKey,TValue}"/>.</item>
/// <item>An object whose type is not the field's (a <c>Town</c> in a <c>MapObject</c> field, a decision, an event) carries its type in
/// <c>"$t"</c>; only types of the game's own assemblies are read back, never anything the message names elsewhere.</item>
/// <item>An object met twice in one piece is written once and then as <c>{"$ref": n}</c>, so shared objects stay shared.</item>
/// </list>
/// </remarks>
internal sealed class StateCodec(GameState catalogs)
{
    private const string TypeKey = "$t";
    private const string ValueKey = "$v";
    private const string RefKey = "$ref";
    private const string DefinitionKey = "$def";

    private static readonly Assembly DomainAssembly = typeof(GameState).Assembly;
    private static readonly Dictionary<Type, FieldInfo[]> FieldsByType = [];
    private static readonly Dictionary<string, Type?> TypesByName = [];
    private static readonly Lock Gate = new();

    /// <summary>Writes <paramref name="value"/> as declared to be a <typeparamref name="T"/>.</summary>
    public JsonNode? Write<T>(T value) => new Writer(catalogs).Write(value, typeof(T));

    /// <summary>Reads a <typeparamref name="T"/> written by <see cref="Write{T}"/>.</summary>
    public T? Read<T>(JsonNode? node) => (T?)new Reader(catalogs).Read(node, typeof(T));

    /// <summary>
    /// Gives <paramref name="target"/> every field of <paramref name="source"/>, an object of the same type just read: the views that hold the
    /// target (a hero's figure on the map, an open town screen) see the new values.
    /// </summary>
    public static void CopyInto(object target, object source)
    {
        if (target.GetType() != source.GetType())
        {
            throw new ArgumentException($"A {source.GetType().Name} cannot be copied into a {target.GetType().Name}.", nameof(source));
        }

        foreach (var field in FieldsOf(target.GetType()))
        {
            field.SetValue(target, field.GetValue(source));
        }
    }

    private static FieldInfo[] FieldsOf(Type type)
    {
        lock (Gate)
        {
            if (FieldsByType.TryGetValue(type, out var known))
            {
                return known;
            }

            var fields = new List<FieldInfo>();
            for (var t = type; t is not null && t != typeof(object) && t != typeof(ValueType); t = t.BaseType)
            {
                fields.InsertRange(0, t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f => !typeof(Delegate).IsAssignableFrom(f.FieldType))
                    .OrderBy(f => f.MetadataToken));
            }

            known = [.. fields];
            FieldsByType[type] = known;
            return known;
        }
    }

    /// <summary>The name a field is written under: its own, or with its declaring type's when a base type has a field of that name too.</summary>
    private static string KeyOf(FieldInfo[] fields, int index)
    {
        var field = fields[index];
        return fields.Count(f => f.Name == field.Name) > 1 ? $"{field.DeclaringType!.Name}.{field.Name}" : field.Name;
    }

    private static bool IsScalar(Type type) =>
        type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);

    private static Type? ElementOfSequence(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() is var definition &&
            (definition == typeof(List<>) || definition == typeof(HashSet<>) || definition == typeof(SortedSet<>) || definition == typeof(Queue<>)))
        {
            return type.GetGenericArguments()[0];
        }

        return null;
    }

    private static (Type Key, Type Value)? EntriesOf(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() is var definition &&
            (definition == typeof(Dictionary<,>) || definition == typeof(SortedDictionary<,>)))
        {
            var arguments = type.GetGenericArguments();
            return (arguments[0], arguments[1]);
        }

        return null;
    }

    /// <summary>For a field typed as a collection interface: its item type, or its key and value types.</summary>
    private static (Type? Item, (Type Key, Type Value)? Entry) InterfaceShape(Type type)
    {
        if (!type.IsInterface)
        {
            return (null, null);
        }

        foreach (var candidate in type.GetInterfaces().Prepend(type))
        {
            if (candidate.IsGenericType)
            {
                var definition = candidate.GetGenericTypeDefinition();
                if (definition == typeof(IReadOnlyDictionary<,>) || definition == typeof(IDictionary<,>))
                {
                    var arguments = candidate.GetGenericArguments();
                    return (null, (arguments[0], arguments[1]));
                }
            }
        }

        foreach (var candidate in type.GetInterfaces().Prepend(type))
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return (candidate.GetGenericArguments()[0], null);
            }
        }

        return (null, null);
    }

    private static string NameOf(Type type) =>
        type.Assembly == DomainAssembly && !type.IsGenericType && type.FullName is { } name
            ? name
            : throw new NotSupportedException($"A LAN view cannot carry a {type} where a {type.BaseType} is expected.");

    private static Type TypeNamed(string name, Type declared)
    {
        Type? type;
        lock (Gate)
        {
            if (!TypesByName.TryGetValue(name, out type))
            {
                type = DomainAssembly.GetType(name, throwOnError: false);
                TypesByName[name] = type;
            }
        }

        return type is not null && declared.IsAssignableFrom(type) && !type.IsAbstract
            ? type
            : throw new InvalidDataException($"The host sent a {name} where a {declared.Name} belongs.");
    }

    private sealed class Writer(GameState catalogs)
    {
        private readonly Dictionary<object, int> _seen = new(ReferenceEqualityComparer.Instance);

        public JsonNode? Write(object? value, Type declared)
        {
            if (value is null)
            {
                return null;
            }

            var type = value.GetType();
            if (IsScalar(type))
            {
                return Scalar(value, type);
            }

            if (Nullable.GetUnderlyingType(declared) is { } underlying)
            {
                declared = underlying;
            }

            switch (value)
            {
                case MapObjectDefinition definition when catalogs.ObjectCatalog.TryGet(definition.Id, out var known) && ReferenceEquals(known, definition):
                    return new JsonObject { [DefinitionKey] = definition.Id };
                case CreatureDefinition creature when catalogs.Creatures.TryGet(creature.Id, out var known) && ReferenceEquals(known, creature):
                    return new JsonObject { [DefinitionKey] = creature.Id };
                case GameState or IEventBus or Type:
                    return null;
            }

            if (!type.IsValueType)
            {
                if (_seen.TryGetValue(value, out var index))
                {
                    return new JsonObject { [RefKey] = index };
                }

                _seen[value] = _seen.Count;
            }

            if (ElementOfSequence(type) is { } element)
            {
                return Sequence((IEnumerable)value, element);
            }

            if (EntriesOf(type) is { } entry)
            {
                return Entries((IEnumerable)value, entry.Key, entry.Value);
            }

            if (declared.IsInterface && InterfaceShape(declared) is var shape && (shape.Item is not null || shape.Entry is not null))
            {
                return shape.Entry is { } pair ? Entries((IEnumerable)value, pair.Key, pair.Value) : Sequence((IEnumerable)value, shape.Item!);
            }

            var fields = FieldsOf(type);
            var node = new JsonObject();
            for (var i = 0; i < fields.Length; i++)
            {
                node[KeyOf(fields, i)] = Write(fields[i].GetValue(value), fields[i].FieldType);
            }

            return type == declared ? node : new JsonObject { [TypeKey] = NameOf(type), [ValueKey] = node };
        }

        private static JsonNode Scalar(object value, Type type) => type switch
        {
            _ when type.IsEnum => JsonValue.Create(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture)),
            _ when type == typeof(string) => JsonValue.Create((string)value),
            _ when type == typeof(bool) => JsonValue.Create((bool)value),
            _ when type == typeof(char) => JsonValue.Create((int)(char)value),
            _ when type == typeof(ulong) => JsonValue.Create(((ulong)value).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            _ when type == typeof(double) || type == typeof(float) || type == typeof(decimal) =>
                JsonValue.Create(Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture)),
            _ => JsonValue.Create(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture)),
        };

        private JsonArray Sequence(IEnumerable items, Type element)
        {
            var array = new JsonArray();
            foreach (var item in items)
            {
                array.Add(Write(item, element));
            }

            return array;
        }

        private JsonArray Entries(IEnumerable entries, Type keyType, Type valueType)
        {
            var array = new JsonArray();
            foreach (var entry in entries)
            {
                var type = entry.GetType();
                var key = type.GetProperty("Key")!.GetValue(entry);
                var value = type.GetProperty("Value")!.GetValue(entry);
                array.Add(new JsonArray(Write(key, keyType), Write(value, valueType)));
            }

            return array;
        }
    }

    private sealed class Reader(GameState catalogs)
    {
        private readonly List<object> _seen = [];

        public object? Read(JsonNode? node, Type declared)
        {
            if (node is null)
            {
                return declared == typeof(GameState) ? catalogs : null;
            }

            if (Nullable.GetUnderlyingType(declared) is { } underlying)
            {
                declared = underlying;
            }

            if (IsScalar(declared))
            {
                return Scalar(node, declared);
            }

            if (node is JsonObject reference && reference.TryGetPropertyValue(RefKey, out var index) && reference.Count == 1)
            {
                return _seen[index!.GetValue<int>()];
            }

            if (node is JsonObject definition && definition.TryGetPropertyValue(DefinitionKey, out var id) && definition.Count == 1)
            {
                var name = id!.GetValue<string>();
                return declared == typeof(CreatureDefinition)
                    ? catalogs.Creatures.TryGet(name, out var creature) ? creature : throw Unknown(name)
                    : catalogs.ObjectCatalog.TryGet(name, out var placed) ? placed : throw Unknown(name);
            }

            var type = declared;
            if (node is JsonObject typed && typed.TryGetPropertyValue(TypeKey, out var typeName) && typed.Count == 2)
            {
                type = TypeNamed(typeName!.GetValue<string>(), declared);
                node = typed[ValueKey];
            }

            if (type.IsArray)
            {
                var items = (JsonArray)node!;
                var array = Array.CreateInstance(type.GetElementType()!, items.Count);
                Remember(array);
                for (var i = 0; i < items.Count; i++)
                {
                    array.SetValue(Read(items[i], type.GetElementType()!), i);
                }

                return array;
            }

            if (type.IsInterface)
            {
                var (item, entry) = InterfaceShape(type);
                type = entry is { } pair ? typeof(Dictionary<,>).MakeGenericType(pair.Key, pair.Value)
                    : item is not null && type.IsGenericType && type.GetGenericTypeDefinition() is var shape
                        && (shape == typeof(IReadOnlySet<>) || shape == typeof(ISet<>)) ? typeof(HashSet<>).MakeGenericType(item)
                    : item is not null ? typeof(List<>).MakeGenericType(item)
                    : throw new InvalidDataException($"The host sent something for a {type.Name}, which a view cannot hold.");
            }

            if (ElementOfSequence(type) is { } element)
            {
                var collection = Create(type, element);
                Remember(collection);
                var add = type.GetMethod(type.GetGenericTypeDefinition() == typeof(Queue<>) ? "Enqueue" : "Add", [element])!;
                foreach (var item in (JsonArray)node!)
                {
                    add.Invoke(collection, [Read(item, element)]);
                }

                return collection;
            }

            if (EntriesOf(type) is { } entries)
            {
                var dictionary = (IDictionary)Create(type, entries.Key);
                Remember(dictionary);
                foreach (var pair in (JsonArray)node!)
                {
                    var both = (JsonArray)pair!;
                    dictionary[Read(both[0], entries.Key)!] = Read(both[1], entries.Value);
                }

                return dictionary;
            }

            if (type.Assembly != DomainAssembly && type.Assembly != typeof(StateCodec).Assembly && !(type.IsGenericType && type.Namespace == "System"))
            {
                throw new InvalidDataException($"A view holds no {type}.");
            }

            var value = RuntimeHelpers.GetUninitializedObject(type);
            if (!type.IsValueType)
            {
                Remember(value);
            }

            var fields = FieldsOf(type);
            var members = (JsonObject)node!;
            for (var i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                if (members.TryGetPropertyValue(KeyOf(fields, i), out var member))
                {
                    field.SetValue(value, Read(member, field.FieldType));
                }
                else if (field.FieldType == typeof(GameState))
                {
                    field.SetValue(value, catalogs);
                }
            }

            return value;
        }

        /// <summary>A new, empty collection; sets and dictionaries of strings compare them ordinally, as the rules' own do.</summary>
        private static object Create(Type type, Type key) =>
            key == typeof(string) && type.GetConstructor([typeof(IComparer<string>)]) is { } sorted ? sorted.Invoke([StringComparer.Ordinal])
            : key == typeof(string) && type.GetConstructor([typeof(IEqualityComparer<string>)]) is { } hashed ? hashed.Invoke([StringComparer.Ordinal])
            : Activator.CreateInstance(type)!;

        private void Remember(object value) => _seen.Add(value);

        private static InvalidDataException Unknown(string definition) =>
            new($"The host's game has a {definition} this computer's game does not know: use the same version of the game and its content.");

        private static object Scalar(JsonNode node, Type type) => type switch
        {
            _ when type.IsEnum => Enum.ToObject(type, node.GetValue<long>()),
            _ when type == typeof(string) => node.GetValue<string>(),
            _ when type == typeof(bool) => node.GetValue<bool>(),
            _ when type == typeof(char) => (char)node.GetValue<int>(),
            _ when type == typeof(ulong) => ulong.Parse(node.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture),
            _ when type == typeof(double) => node.GetValue<double>(),
            _ when type == typeof(float) => (float)node.GetValue<double>(),
            _ when type == typeof(decimal) => (decimal)node.GetValue<double>(),
            _ => Convert.ChangeType(node.GetValue<long>(), type, System.Globalization.CultureInfo.InvariantCulture),
        };
    }
}
