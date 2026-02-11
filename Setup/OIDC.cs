using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Web;
using Memoria.Exceptions;
using Memoria.Models;
using Memoria.Models.Config;
using Memoria.Services;
using Memoria.Utils;

namespace Memoria.Setup;

public class ConfigureOidcOptions(IOptions<OAuthConfig> oidcConfig, IKeyService keyService) : IConfigureNamedOptions<OpenIdConnectOptions>
{
	public void Configure(OpenIdConnectOptions options) => Configure(Options.DefaultName, options);

	
	private async Task OnTicketReceived(TicketReceivedContext context)
	{
		context.HandleResponse();

		var sub = context.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
		var nickname = context.Principal.FindFirstValue(JwtRegisteredClaimNames.Name);
		var username = context.Principal.FindFirstValue(JwtRegisteredClaimNames.PreferredUsername);
		var email = context.Principal.FindFirstValue(JwtRegisteredClaimNames.Email);
		var image = context.Principal.FindFirstValue(JwtRegisteredClaimNames.Picture);
		
		if (sub == null)
		{
			var err = new LoginFailedApiException(new Exception("client id not available"));
			context.HttpContext.Response.StatusCode = (int) err.StatusCode;
			await context.HttpContext.Response.WriteAsJsonAsync(err.Value);
			return;
		}
		var transferToken = TrustedDataProvider.SignAndFormatData(new OidcProfileData()
		{
			Subject = sub,
			Username = username,
			Nickname =  nickname,
			EMail =	email,
			Image =	image,
			Provider = context.Scheme.Name
		}, keyService.OidcTransferTokenKey);
		
		var uriBuilder = new UriBuilder(context.ReturnUri);
		
		var query = HttpUtility.ParseQueryString(uriBuilder.Query);
		query["token"] = transferToken;
		uriBuilder.Query = query.ToString();
		var finalUrl = uriBuilder.ToString();
		
		context.Response.Redirect(finalUrl);
	}
	
	public void Configure(string? name, OpenIdConnectOptions options)
	{
		var idpConfig = oidcConfig.Value.IdentityProviders.First(i => i.Identifier == name);

		options.Authority = idpConfig.Authority;
		options.ClientId = idpConfig.ClientId;
		options.ClientSecret = idpConfig.ClientSecret;
		options.CallbackPath = idpConfig.CallbackPath;
		
		options.ResponseType = OpenIdConnectResponseType.Code;
		
		options.GetClaimsFromUserInfoEndpoint = true;
		
		options.ClaimActions.MapUniqueJsonKey(JwtRegisteredClaimNames.PreferredUsername, "preferred_username");
		options.ClaimActions.MapUniqueJsonKey(JwtRegisteredClaimNames.Picture, "picture");
		
		options.Scope.Add("profile");
		options.Scope.Add("email");
		
		options.Events = new OpenIdConnectEvents
		{
			OnTicketReceived = this.OnTicketReceived,
		};
	}
}