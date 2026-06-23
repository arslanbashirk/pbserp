using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PBS.ERP.Infrastructure.Interfaces;
using PBS.ERP.Modules.Survey.Services;
using static PBS.ERP.Shared.Models.SurveyModel;

namespace PBS.ERP.Modules.Survey.Controllers
{

    [Authorize]
    [Route("[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class EntryController : Controller
    {
        private readonly IMetadata _metadata;

        public EntryController(IMetadata metadata)
        {
            _metadata = metadata;
        }

        [HttpGet("Form")]
        public async Task<IActionResult> Form(string form)
        {
            //ViewBag.Survey = parms.survey;
            //ViewBag.Area = parms.area;
            MetadataViewModel view= new MetadataViewModel();
            
            //Temporay Block Opening
            AreaViewModel area= new AreaViewModel();
            area.AreaCode = "900001";
            view.Area=area;
            //Temporary Block Closing

            view.Entity = await _metadata.GetEntityAsync(form);
            if (view.Entity != null)
            {
                string table = "[" + view.Entity.Database + "].[" + view.Entity.Schema + "].[" +view.Entity.Name +"]";
                view.Entry = await _metadata.GetFormDataAsync(table," AND {alias}.AreaCode="+view.Area.AreaCode);
            }
            view.Form = await _metadata.GetFormTitlesAsync(form);
            view.Fields = await _metadata.GetFieldsAsync(form);
            if(view.Form != null)
            {   
                ViewBag.Survey= view.Form.SurveyUID;
                view.NextPrevious = await _metadata.GetNextandPrevious(view.Form.SurveyUID,form);
                if (view.Form.FormType.Equals("GRID"))
                {
                    return View("~/Views/Entry/Grid.cshtml", view);
                }
                else if (view.Form.FormType.Equals("REPEAT"))
                {
                    return View("~/Views/Entry/Repeat.cshtml", view);
                }
            }
            
            return View(view);

        }

        
    }
}
