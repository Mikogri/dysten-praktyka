using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;

namespace DystenSoftwateManagment.Pages
{
	[ValidateAntiForgeryToken]
	public class LoginModel(SignInManager<IdentityUser> signInManager) : PageModel
	{
		private readonly SignInManager<IdentityUser> _signInManager = signInManager;

		[BindProperty]
		public string Email { get; set; }

		[BindProperty]
		public string Password { get; set; }

		public string ErrorMessage { get; set; }

		public IActionResult OnGet()
		{
			if (User.Identity?.IsAuthenticated == true)
			{
				return Redirect("/projects");
			}

			return Page();
		}

		public async Task<IActionResult> OnPostAsync()
		{
			if (!ModelState.IsValid)
			{
				return Page();
			}

			var result = await _signInManager.PasswordSignInAsync(Email, Password, isPersistent: true, lockoutOnFailure: false);

			if (result.Succeeded)
			{
				return Redirect("/projects");
			}

			ErrorMessage = "Nieprawid³owy login lub has³o";
			return Page();
		}
	}
}
