using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Software_Engineering_2328668.Controllers
{
    [Authorize(Roles = "patient")]
    public class PatientController : Controller
    {
        public IActionResult Dashboard() => View();
    }
}
