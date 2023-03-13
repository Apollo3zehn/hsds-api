using Microsoft.AspNetCore.Mvc;

namespace Hsds.SpecGenerator.Controllers;

[ApiController]
[Route("[controller]")]
public class DomainController : ControllerBase
{
    [HttpGet]
    public string GetDomain() => throw new NotImplementedException();
}