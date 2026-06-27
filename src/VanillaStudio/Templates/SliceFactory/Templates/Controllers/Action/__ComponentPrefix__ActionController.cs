using Microsoft.AspNetCore.Mvc;
using {{RootNamespace}}.ServiceContracts.Features.__moduleNamespace__;

namespace {{RootNamespace}}.Controllers;

[ApiController]
[Route("api/[controller]")]
public class __ComponentPrefix__ActionController : ControllerBase
{
    private readonly I__ComponentPrefix__ActionDataService _service;

    public __ComponentPrefix__ActionController(I__ComponentPrefix__ActionDataService service)
        => _service = service;

    [HttpPost("{id}/execute")]
    public async Task<IActionResult> ExecuteAsync(__primaryKeyType__ id)
    {
        await _service.ExecuteAsync(id);
        return NoContent();
    }
}
