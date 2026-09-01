using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs.Auth;

namespace TmsApi.Api.Controllers;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login(
        [FromBody] LoginRequest request,
        [FromServices] IWebHostEnvironment env)
    {
        // Validate credentials (demo account for M10 transport testing)
        if (request.Username == "admin" && request.Password == "Password123!")
        {
            var dummyJwt = "header.payload.signature-demo-token";

            // Append HttpOnly authentication cookie (JavaScript CANNOT read this token)
            Response.Cookies.Append("tms_auth", dummyJwt, new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(), // HTTPS in prod; HTTP permitted locally over dev
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddHours(2)
            });

            return Ok(new UserProfileDto("System Admin", "Admin"));
        }

        return Unauthorized(new { detail = "Invalid username or password." });
    }

    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        // Inspect cookie attached automatically by the browser on cross-origin requests
        if (Request.Cookies.TryGetValue("tms_auth", out _))
        {
            return Ok(new UserProfileDto("System Admin", "Admin"));
        }

        return Unauthorized(new { detail = "Session expired or missing authentication cookie." });
    }
}