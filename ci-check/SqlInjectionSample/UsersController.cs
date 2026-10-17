using System.Data;
using Microsoft.AspNetCore.Mvc;

namespace CiCheck.SqlInjectionSample;

[ApiController]
[Route("users")]
public sealed class UsersController(IDbConnection connection) : ControllerBase
{
    [HttpGet]
    public IActionResult Find([FromQuery] string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM users WHERE name = '" + name + "'"; // codeql[cs/sql-injection] amostra da SPEC-0013 para confirmar a supressão
        return Ok(command.ExecuteScalar());
    }
}
