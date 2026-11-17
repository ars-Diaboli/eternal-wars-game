using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Placeholder meshes and their materials, made once per shape, size and colour and shared by every node that draws them. A view keeps its own
/// cache, so the meshes go when the view does.
/// </summary>
internal sealed class MeshCache
{
    private readonly Dictionary<(Color Color, bool Unshaded), StandardMaterial3D> _materials = [];
    private readonly Dictionary<(char Shape, Vector3 Size, Color Color, bool Unshaded), PrimitiveMesh> _meshes = [];

    public BoxMesh Box(Vector3 size, Color color) =>
        (BoxMesh)Mesh('b', size, color, false, () => new BoxMesh { Size = size });

    /// <summary>A cylinder of <paramref name="top"/> and <paramref name="bottom"/> radius (0 on top: a cone).</summary>
    public CylinderMesh Cylinder(float top, float bottom, float height, Color color, bool unshaded = false) =>
        (CylinderMesh)Mesh('c', new Vector3(top, bottom, height), color, unshaded,
            () => new CylinderMesh { TopRadius = top, BottomRadius = bottom, Height = height });

    public SphereMesh Sphere(float radius, Color color) =>
        (SphereMesh)Mesh('s', new Vector3(radius, 0, 0), color, false, () => new SphereMesh { Radius = radius, Height = radius * 2 });

    private PrimitiveMesh Mesh(char shape, Vector3 size, Color color, bool unshaded, Func<PrimitiveMesh> make)
    {
        if (!_meshes.TryGetValue((shape, size, color, unshaded), out var mesh))
        {
            mesh = make();
            mesh.Material = Material(color, unshaded);
            _meshes[(shape, size, color, unshaded)] = mesh;
        }

        return mesh;
    }

    private StandardMaterial3D Material(Color color, bool unshaded)
    {
        if (!_materials.TryGetValue((color, unshaded), out var material))
        {
            material = new StandardMaterial3D { AlbedoColor = color };
            if (unshaded)
            {
                material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            }

            _materials[(color, unshaded)] = material;
        }

        return material;
    }
}
