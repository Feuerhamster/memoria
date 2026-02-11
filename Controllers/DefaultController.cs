using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Memoria.Exceptions;
using Memoria.Services;
using System.Security.Claims;

namespace Memoria.Controllers;

[ApiController]
[Route("/")]
public class DefaultController() : ControllerBase {

	[HttpHead]
	public IActionResult OnlineCheck() {
		return Ok();
	}
	
	[HttpGet("healthcheck")]
	public async Task<IActionResult> HealthCheck()
	{
		return Ok();
	}
}