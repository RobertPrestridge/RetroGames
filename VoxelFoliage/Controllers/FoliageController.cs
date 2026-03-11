using Microsoft.AspNetCore.Mvc;

namespace VoxelFoliage.Controllers;

[Route("foliage")]
public class FoliageController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }
}
