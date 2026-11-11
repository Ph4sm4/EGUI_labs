using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace OutdoorRental.Models.ViewModels;

// state of the login/register modals. bootstrap opens them normally, but we render one
// already open after a failed attempt or when the url has ?auth=...
public class AuthModalsViewModel
{
    public const string TempDataModal = "AuthModal";
    public const string TempDataErrors = "AuthErrors";
    public const string TempDataLogin = "AuthLogin";
    public const string TempDataRegister = "AuthRegister";

    public LoginViewModel Login { get; set; } = new();
    public RegisterViewModel Register { get; set; } = new();

    // "login", "register" or null
    public string? OpenModal { get; set; }

    public List<string> Errors { get; set; } = new();

    // this page minus the auth stuff in the query, used for the close button
    public string CurrentUrl { get; set; } = "/";

    public bool IsLoginOpen => OpenModal == "login";
    public bool IsRegisterOpen => OpenModal == "register";
    public bool IsServerOpen => OpenModal != null;

    public static AuthModalsViewModel Build(HttpContext http, ITempDataDictionary tempData)
    {
        var request = http.Request;
        var query = request.Query
            .Where(q => q.Key != "auth" && q.Key != "returnUrl")
            .Select(q => new KeyValuePair<string, string?>(q.Key, q.Value.ToString()));
        var currentUrl = $"{request.PathBase}{request.Path}{QueryString.Create(query)}";

        var model = new AuthModalsViewModel { CurrentUrl = currentUrl };

        // a failed attempt left us a note in tempdata
        if (tempData[TempDataModal] is string failed)
        {
            model.OpenModal = failed;
            model.Errors = Deserialize<List<string>>(tempData[TempDataErrors]) ?? new();
            model.Login = Deserialize<LoginViewModel>(tempData[TempDataLogin]) ?? new();
            model.Register = Deserialize<RegisterViewModel>(tempData[TempDataRegister]) ?? new();
        }
        else
        {
            var auth = request.Query["auth"].ToString();
            if (auth is "login" or "register")
                model.OpenModal = auth;
        }

        // after login go to the page that asked for it, otherwise just stay here
        var returnUrl = request.Query["returnUrl"].ToString();
        if (string.IsNullOrEmpty(model.Login.ReturnUrl))
            model.Login.ReturnUrl = IsLocal(returnUrl) ? returnUrl : currentUrl;
        if (string.IsNullOrEmpty(model.Register.ReturnUrl))
            model.Register.ReturnUrl = IsLocal(returnUrl) ? returnUrl : currentUrl;
        model.Login.Origin = currentUrl;
        model.Register.Origin = currentUrl;

        return model;
    }

    // local = starts with one slash, so no //evil.com tricks
    public static bool IsLocal(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");

    private static T? Deserialize<T>(object? value) =>
        value is string json ? JsonSerializer.Deserialize<T>(json) : default;
}
