using Microsoft.AspNetCore.Mvc;

namespace PedalAcrossCanada.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
}
