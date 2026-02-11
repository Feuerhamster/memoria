namespace Memoria.Exceptions;

public class SessionNotFoundException() : Exception("session not found");

public class SessionInvalidClientException() : Exception("request client does not match session client");

public class SessionNotRenewedException() : Exception("session renewal failed in database");