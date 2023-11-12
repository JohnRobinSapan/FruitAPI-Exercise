using Microsoft.AspNetCore.Authentication;

public class ApiKeyAuthenticationSchemeOptions: AuthenticationSchemeOptions  {
    public string ApiKey {get; set;}
}