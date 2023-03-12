using Microsoft.AspNetCore.Mvc;

namespace Hsds.Specification.Controllers;

[ApiController]
[Route("[domain]")]
public class DomainController : ControllerBase
{
    public IAction<string> GetDomain() => throw new NotImplementedException();
}