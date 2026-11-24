using BetaTornBot.Modules.WarModule.WarCalcs.WarPay;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using WarPay;

public static class WarPayHtml
{
    private const int MaxLen = 1800;
    private static readonly CultureInfo inv = CultureInfo.InvariantCulture;

    // Colours (same as the template)
    private const string Bg = "#171b20";
    private const string Panel = "#242a30";
    private const string RowAlt = "#2b3138";
    private const string HeaderRow = "#343b42";
    private const string Border = "#454b52";
    private const string RowBorder = "#383e44";
    private const string Gold = "#e7c77c";
    private const string GoldDark = "#c49a45";

    private const string OuterOpen =
        "<div style='max-width:750px;margin:15px auto;padding:20px;background:" + Bg +
        ";border:1px solid " + Border + ";border-radius:8px;color:#ddd;font-family:Arial,sans-serif'>";

    // Member panel opening + title + column headers (all divs, no tables)
    private static readonly string MemberStart =
        "<div style='background:" + Panel + ";border:1px solid " + Border + ";border-radius:5px;padding:10px'>" +
        "<div style='text-align:center;font-size:13px;font-weight:bold;letter-spacing:2px;color:" + Gold + ";padding:10px 0 15px 0'>MEMBER PAYOUTS</div>" +
        "<div style='background:" + HeaderRow + ";padding:12px 8px;border-bottom:2px solid " + GoldDark + ";font-size:0'>" +
        Cell("NAME", 38, "13px", "font-weight:bold;color:" + Gold) +
        Cell("SCORE", 17, "13px", "font-weight:bold;color:" + Gold + ";text-align:right") +
        Cell("PAY", 45, "13px", "font-weight:bold;color:" + Gold + ";text-align:right") +
        "</div>";

    private const string End = "</div></div>"; // member panel + outer wrapper

    /// <summary>
    /// Builds ONE html document and cuts it into pieces of at most 1800 chars.
    /// Cuts only fall between elements, never inside a tag.
    /// string.Concat(result) gives back the complete document.
    /// </summary>
    public static List<string> Build(
        WarPayResult result,
        decimal totalCacheValue,
        decimal factionTake,   // whole-number percent: 20 = 20%
        bool fullstats,
        string subtitle = "")
    {
        decimal payoutPool = totalCacheValue - (totalCacheValue * factionTake / 100m);

        var pieces = new List<string> { OuterOpen, HeaderBlock(subtitle) };

        if (fullstats)
            pieces.Add(SummaryBlock(totalCacheValue, factionTake, payoutPool));

        pieces.Add(MemberStart);

        var players = result.Players.ToList();
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            string bg = i % 2 == 0 ? Panel : RowAlt;
            string border = i == players.Count - 1 ? "" : "border-bottom:1px solid " + RowBorder + ";";

            pieces.Add(
                "<div style='background:" + bg + ";padding:10px 8px;" + border + "font-size:0'>" +
                Cell(Enc(p.Name), 38, "13px", "") +
                Cell(p.Score.ToString("N0", inv), 17, "13px", "text-align:right") +
                Cell("$" + p.Payout.ToString("N0", inv), 45, "13px", "text-align:right;color:" + Gold) +
                "</div>");
        }

        pieces.Add(End);

        // Pack greedily into chunks <= MaxLen
        var chunks = new List<string>();
        var cur = new StringBuilder();
        foreach (var piece in pieces)
        {
            if (cur.Length > 0 && cur.Length + piece.Length > MaxLen)
            {
                chunks.Add(cur.ToString());
                cur.Clear();
            }
            cur.Append(piece);
        }
        if (cur.Length > 0) chunks.Add(cur.ToString());

        return chunks;
    }

    private static string HeaderBlock(string subtitle)
    {
        var sb = new StringBuilder();
        sb.Append("<div style='text-align:center;padding:15px 10px;border-bottom:2px solid " + GoldDark + ";margin-bottom:20px'>");
        sb.Append("<div style='font-size:28px;font-weight:bold;letter-spacing:4px;color:#fff'>WAR PAY RESULTS</div>");
        if (!string.IsNullOrWhiteSpace(subtitle))
            sb.Append("<div style='font-size:12px;color:#999;margin-top:6px;letter-spacing:2px'>" + Enc(subtitle.ToUpperInvariant()) + "</div>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string SummaryBlock(decimal cache, decimal take, decimal pool)
    {
        var sb = new StringBuilder();
        sb.Append("<div style='background:" + Panel + ";border:1px solid " + Border + ";border-radius:5px;padding:15px;margin-bottom:25px'>");
        sb.Append("<div style='text-align:center;font-size:13px;font-weight:bold;letter-spacing:2px;color:" + Gold + ";padding:0 0 15px 0'>WAR PAYOUT SUMMARY</div>");
        sb.Append(SummaryRow("Total Cache Value", "$" + cache.ToString("N0", inv), false));
        sb.Append(SummaryRow("Faction Take", take.ToString("0.##", inv) + "%", false));
        sb.Append(SummaryRow("Total Member Payout", "$" + pool.ToString("N0", inv), true));
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string SummaryRow(string label, string value, bool big)
    {
        string border = big ? "" : "border-bottom:1px solid " + Border + ";";
        string pad = big ? "12px 5px" : "10px 5px";
        string labelStyle = big ? "font-weight:bold;color:#fff" : "";
        string valueSize = big ? "18px" : "14px";
        return "<div style='padding:" + pad + ";" + border + "font-size:0'>" +
               Cell(label, big ? 48 : 55, "14px", labelStyle) +
               Cell(value, big ? 52 : 45, valueSize, "text-align:right;color:" + Gold + ";font-weight:bold") +
               "</div>";
    }

    private static string Cell(string content, int widthPct, string fontSize, string extraStyle) =>
        "<div style='display:inline-block;width:" + widthPct + "%;font-size:" + fontSize + ";vertical-align:middle" +
        (extraStyle.Length > 0 ? ";" + extraStyle : "") + "'>" + content + "</div>";

    private static string Enc(string s) => WebUtility.HtmlEncode(s ?? "");
}
