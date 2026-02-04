using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Software_Engineering_2328668.Services;

namespace Software_Engineering_2328668.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly CsvIngestionService _ingestion;
        public AdminController(CsvIngestionService ingestion) => _ingestion = ingestion;

        public IActionResult Dashboard() => View();

        [HttpPost]
        public async Task<IActionResult> RunIngestion()
        {
            var (datasets, frames, alerts) = await _ingestion.RunForAllCsvAsync();
            return Content($"Ingestion complete. Datasets: {datasets}, Frames: {frames}, Alerts: {alerts}");
        }
    }
}



/*using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Software_Engineering_2328668.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        public IActionResult Dashboard() => View();
    }
}
*/

