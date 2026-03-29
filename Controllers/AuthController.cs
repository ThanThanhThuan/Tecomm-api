using Microsoft.AspNetCore.Mvc;
using Tecomm.Models;
using Tecomm.Services;

namespace Tecomm.Controllers;

/// <summary>
/// POST /api/auth/register  – create account
/// POST /api/auth/login     – get JWT token
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    // POST /api/auth/register
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "Email and full name are required." });

        var (user, error) = _auth.Register(request);
        if (error is not null)
            return BadRequest(new { message = error });

        // Auto-login after registration
        var (response, loginError) = _auth.Login(new LoginRequest
        {
            Email    = request.Email,
            Password = request.Password
        });

        return loginError is not null
            ? BadRequest(new { message = loginError })
            : CreatedAtAction(nameof(Register), response);
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var (response, error) = _auth.Login(request);
        return error is not null
            ? Unauthorized(new { message = error })
            : Ok(response);
    }
}
