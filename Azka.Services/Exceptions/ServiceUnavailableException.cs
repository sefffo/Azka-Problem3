namespace Azka.Services.Exceptions;

public class ServiceUnavailableException : Exception
{
    public ServiceUnavailableException(string message) : base(message) { }
}
