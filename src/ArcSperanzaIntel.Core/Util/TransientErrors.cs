using System.Net.Sockets;

namespace ArcSperanzaIntel.Core.Util;

/// <summary>
/// Błędy „przejściowe”: przerwane / anulowane pobieranie, brak sieci, timeout. Aplikacja sobie z nimi radzi
/// (dane z pamięci podręcznej, ponowna próba), więc nie zapisujemy ich do crash.log — inaczej zaśmiecałyby zgłoszenia błędów.
/// </summary>
public static class TransientErrors
{
    public static bool IsTransient(Exception? ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is OperationCanceledException or HttpRequestException or SocketException or TimeoutException) return true;
            if (e is AggregateException agg && agg.InnerExceptions.Count > 0 && agg.InnerExceptions.All(IsTransient)) return true;
        }
        return false;
    }
}
