using Microsoft.AspNetCore.Mvc;
using Memoria.Exceptions;

namespace Memoria.Controllers;

[ApiController]
[Route("/")]
public class DefaultController(AppDbContext db) : ControllerBase {

	[HttpHead]
	public IActionResult OnlineCheck() {
		return Ok();
	}

	[HttpGet("healthcheck")]
	public async Task<IActionResult> HealthCheck(CancellationToken ct)
	{
		var canConnect = await db.Database.CanConnectAsync(ct);

		if (!canConnect)
		{
			return new ApplicationUnhealthyApiException("database not reachable");
		}

		return Ok();
	}
}
