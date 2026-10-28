namespace HrFlow.Api.Security;

public class BusinessRuleException(string message) : Exception(message);

public class NotFoundException(string message) : Exception(message);

public class ForbiddenAccessException(string message = "Accès refusé.") : Exception(message);
