namespace MiduX.Exceptions
{
    public class MediatorException(string message, Exception innerException) : Exception(message, innerException)
    {
    }
}
