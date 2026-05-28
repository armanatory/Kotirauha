using System.Net;
using System.Text;

namespace Kotirauha.Infrastructure.Services;

// Branded HTML + plain-text email bodies. Inline CSS only (email clients strip
// <style> blocks), single-table layout (Outlook chokes on flex/grid).
public static class EmailTemplates
{
    private const string Accent = "#0f766e";      // teal-700 (brand)
    private const string CodeBg = "#ecfdf5";       // emerald-50
    private const string CodeBorder = "#99f6e4";   // teal-200
    private const string ApexUrl = "https://kotirauha.xyz";

    public static (string Subject, string Html, string Text) RenderMagicLink(string link, string code, string lang)
    {
        var fi = lang == "fi";
        var subject = fi ? "Kirjautumislinkkisi Kotirauhaan" : "Your Kotirauha login link";
        var slogan = fi
            ? "Rauhallinen ja puolueeton kirjaus taloyhtiön arjesta."
            : "A calm, neutral record of residential peace.";
        var heading = fi ? "Kirjaudu Kotirauhaan" : "Sign in to Kotirauha";
        var intro = fi
            ? "Käytä jompaakumpaa tapaa kirjautuaksesi. Molemmat vanhenevat 20 minuutissa."
            : "Use either option below to sign in. Both expire in 20 minutes.";
        var codeLabel = fi ? "Syötä tämä koodi sovellukseen:" : "Enter this code in the app:";
        var linkLabel = fi ? "Tai avaa tämä linkki:" : "Or open this link directly:";
        var button = fi ? "Kirjaudu sisään" : "Sign in";
        var copyLabel = fi ? "Tai kopioi tämä osoite selaimeen:" : "Or copy this address into your browser:";
        var ignore = fi
            ? "Jos et pyytänyt tätä viestiä, voit jättää sen huomiotta."
            : "If you didn't request this, you can safely ignore this message.";

        var inner = $@"
<h1 style=""margin:0 0 12px;font:600 22px/1.3 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#0f172a"">{Esc(heading)}</h1>
<p style=""margin:0 0 18px;font:14px/1.55 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#334155"">{Esc(intro)}</p>

<p style=""margin:0 0 8px;font:13px/1.4 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#64748b"">{Esc(codeLabel)}</p>
<div style=""margin:0 0 24px;padding:18px;background:{CodeBg};border:1px solid {CodeBorder};border-radius:8px;text-align:center;font:600 32px/1 'SF Mono',Menlo,Consolas,monospace;letter-spacing:.4em;color:{Accent}"">{Esc(code)}</div>

<p style=""margin:0 0 12px;font:13px/1.4 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#64748b"">{Esc(linkLabel)}</p>
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:0 0 20px"">
  <tr><td>
    <a href=""{Esc(link)}"" style=""display:inline-block;padding:12px 26px;background:{Accent};color:#ffffff;text-decoration:none;border-radius:8px;font:500 14px/1 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif"">{Esc(button)}</a>
  </td></tr>
</table>
<p style=""margin:0 0 18px;font:11px/1.4 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#94a3b8;word-break:break-all"">
  {Esc(copyLabel)}<br><span style=""color:#64748b"">{Esc(link)}</span>
</p>
<p style=""margin:0;font:12px/1.5 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#94a3b8"">{Esc(ignore)}</p>";

        var html = Wrap(subject, slogan, inner);

        var text =
            $"{heading}\n{slogan}\n\n{intro}\n\n" +
            $"  {codeLabel}\n\n      {code}\n\n" +
            $"  {linkLabel}\n\n      {link}\n\n" +
            $"{ignore}\n\n—\nKotirauha · {ApexUrl}\n";

        return (subject, html, text);
    }

    public static (string Subject, string Html, string Text) RenderJoinRequest(
        string buildingName, string requester, string link, string lang)
    {
        var fi = lang == "fi";
        var subject = fi ? "Uusi liittymispyyntö taloyhtiöösi" : "New join request for your building";
        var slogan = fi
            ? "Rauhallinen ja puolueeton kirjaus taloyhtiön arjesta."
            : "A calm, neutral record of residential peace.";
        var heading = fi ? "Uusi liittymispyyntö" : "New join request";
        var body = fi
            ? $"{Esc(requester)} haluaa liittyä taloyhtiöön {Esc(buildingName)}. Voit hyväksyä tai hylätä pyynnön sovelluksessa."
            : $"{Esc(requester)} wants to join {Esc(buildingName)}. You can approve or decline the request in the app.";
        var button = fi ? "Avaa Kotirauha" : "Open Kotirauha";

        var inner = $@"
<h1 style=""margin:0 0 12px;font:600 22px/1.3 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#0f172a"">{Esc(heading)}</h1>
<p style=""margin:0 0 20px;font:14px/1.55 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#334155"">{body}</p>
<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" style=""margin:0 0 8px"">
  <tr><td>
    <a href=""{Esc(link)}"" style=""display:inline-block;padding:12px 26px;background:{Accent};color:#ffffff;text-decoration:none;border-radius:8px;font:500 14px/1 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif"">{Esc(button)}</a>
  </td></tr>
</table>";

        var html = Wrap(subject, slogan, inner);
        var text = fi
            ? $"{heading}\n\n{requester} haluaa liittyä taloyhtiöön {buildingName}.\n\nHyväksy tai hylkää pyyntö: {link}\n\n—\nKotirauha · {ApexUrl}\n"
            : $"{heading}\n\n{requester} wants to join {buildingName}.\n\nApprove or decline: {link}\n\n—\nKotirauha · {ApexUrl}\n";
        return (subject, html, text);
    }

    private static string Wrap(string title, string slogan, string inner)
    {
        var sb = new StringBuilder(2048);
        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\">");
        sb.Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append($"<title>{Esc(title)}</title></head>");
        sb.Append("<body style=\"margin:0;padding:0;background:#f6f7f9\">");
        sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"background:#f6f7f9\"><tr><td align=\"center\">");
        sb.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"560\" style=\"max-width:560px;width:100%;margin:32px auto\">");
        sb.Append("<tr><td style=\"padding:0 4px 16px;text-align:center\">");
        sb.Append($"<span style=\"font:600 20px/1 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:{Accent};letter-spacing:-.01em\">Kotirauha</span>");
        sb.Append($"<div style=\"margin-top:4px;font:12px/1.4 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#64748b\">{Esc(slogan)}</div>");
        sb.Append("</td></tr>");
        sb.Append("<tr><td style=\"padding:28px 28px 24px;background:#ffffff;border:1px solid #e2e8f0;border-radius:12px\">");
        sb.Append(inner);
        sb.Append("</td></tr>");
        sb.Append("<tr><td style=\"padding:18px 8px 0;text-align:center;font:11px/1.5 -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#94a3b8\">");
        sb.Append($"&copy; Kotirauha · <a href=\"{ApexUrl}\" style=\"color:#64748b;text-decoration:none\">kotirauha.xyz</a>");
        sb.Append("</td></tr>");
        sb.Append("</table></td></tr></table></body></html>");
        return sb.ToString();
    }

    private static string Esc(string s) => WebUtility.HtmlEncode(s);
}
