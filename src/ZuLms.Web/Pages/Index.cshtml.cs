using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ZuLms.Web.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet() => Redirect("/login");
}
