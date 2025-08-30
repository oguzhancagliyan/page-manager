namespace PageManager.Shared.Exceptions;
public sealed class NotFoundException(string m) : Exception(m);