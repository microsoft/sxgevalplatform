using Microsoft.AspNetCore.Mvc;
using SxgEvalPlatformApi.Services;

namespace SxgEvalPlatformApi.Controllers
{

    [Route("api/v1/[controller]")]
    public class EvalController : BaseController
    {
        private readonly IEvaluationService _evaluationService;

        public EvalController(IEvaluationService evaluationService, ILogger<EvalController> logger)
            : base(logger)
        {
            _evaluationService = evaluationService;
        }

        //public IActionResult Index()
        //{
        //    //return View();
        //}
    }
}
