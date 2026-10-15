namespace EW.CoreLib.Interfaces;

public interface IPersist<T>
{
   void FromState(T dto);
   T ToState();
}

public interface IPersistOwnable<T>
{
   void FromState(T dto);
   T ToState();
}