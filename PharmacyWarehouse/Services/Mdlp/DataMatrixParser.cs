namespace PharmacyWarehouse.Services.Mdlp;

public class DataMatrixResult
{
    public string? Gtin { get; set; }
    public string? SerialNumber { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? Series { get; set; }
    public string? Sgtin { get; set; }
    public string RawCode { get; set; } = null!;
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class DataMatrixParser
{
    private const char Gs = '\x1D';

    public static DataMatrixResult Parse(string code)
    {
        var result = new DataMatrixResult
        {
            RawCode = code,
            IsValid = false
        };

        if (string.IsNullOrWhiteSpace(code))
        {
            result.ErrorMessage = "Код пустой";
            return result;
        }

        code = code.Replace(Gs.ToString(), Gs.ToString())
                   .Replace(((char)29).ToString(), Gs.ToString());

        try
        {
            int pos = 0;
            while (pos < code.Length)
            {
                if (!TryReadAi(code, ref pos, out var ai))
                    break;

                switch (ai)
                {
                    case "01":
                        if (pos + 14 > code.Length)
                        {
                            result.ErrorMessage = "Недостаточно данных для GTIN (AI 01)";
                            return result;
                        }
                        result.Gtin = code.Substring(pos, 14);
                        pos += 14;
                        break;

                    case "17":
                        if (pos + 6 > code.Length)
                        {
                            result.ErrorMessage = "Недостаточно данных для срока годности (AI 17)";
                            return result;
                        }
                        var dateStr = code.Substring(pos, 6);
                        pos += 6;
                        if (!TryParseExpirationDate(dateStr, out var expDate))
                        {
                            result.ErrorMessage = $"Некорректная дата годности: {dateStr}";
                            return result;
                        }
                        result.ExpirationDate = expDate;
                        break;

                    case "10":
                        result.Series = ReadVariableField(code, ref pos);
                        break;

                    case "21":
                        result.SerialNumber = ReadVariableField(code, ref pos);
                        break;

                    default:
                        SkipUnknownField(code, ref pos, ai);
                        break;
                }
            }

            if (string.IsNullOrEmpty(result.Gtin))
            {
                result.ErrorMessage = "GTIN (AI 01) не найден в коде";
                return result;
            }

            if (!string.IsNullOrEmpty(result.SerialNumber))
            {
                var serial = result.SerialNumber.PadLeft(13, '0');
                if (serial.Length > 13)
                    serial = serial[^13..];
                result.Sgtin = result.Gtin + serial;
            }

            result.IsValid = true;
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private static bool TryReadAi(string code, ref int pos, out string ai)
    {
        ai = string.Empty;
        if (pos >= code.Length)
            return false;

        if (pos + 2 <= code.Length && IsAiPrefix(code.Substring(pos, 2)))
        {
            ai = code.Substring(pos, 2);
            pos += 2;
            return true;
        }

        if (pos + 3 <= code.Length && IsAiPrefix(code.Substring(pos, 3)))
        {
            ai = code.Substring(pos, 3);
            pos += 3;
            return true;
        }

        return false;
    }

    private static bool IsAiPrefix(string prefix)
    {
        return prefix is "01" or "10" or "17" or "21"
            or "00" or "11" or "15" or "20" or "30" or "37";
    }

    private static string ReadVariableField(string code, ref int pos)
    {
        var start = pos;
        while (pos < code.Length)
        {
            if (code[pos] == Gs)
            {
                var value = code.Substring(start, pos - start);
                pos++;
                return value;
            }

            if (pos + 2 <= code.Length && IsAiPrefix(code.Substring(pos, 2)))
                break;

            if (pos + 3 <= code.Length && IsAiPrefix(code.Substring(pos, 3)))
                break;

            pos++;
        }

        return code.Substring(start, pos - start);
    }

    private static void SkipUnknownField(string code, ref int pos, string ai)
    {
        var fixedLengths = new Dictionary<string, int>
        {
            ["00"] = 18, ["11"] = 6, ["15"] = 6, ["20"] = 2, ["30"] = 0
        };

        if (fixedLengths.TryGetValue(ai, out var len) && len > 0 && pos + len <= code.Length)
        {
            pos += len;
            return;
        }

        ReadVariableField(code, ref pos);
    }

    private static bool TryParseExpirationDate(string yymmdd, out DateOnly date)
    {
        date = default;
        if (yymmdd.Length != 6 || !int.TryParse(yymmdd[..2], out int yy)
            || !int.TryParse(yymmdd.Substring(2, 2), out int mm)
            || !int.TryParse(yymmdd.Substring(4, 2), out int dd))
            return false;

        int year = 2000 + yy;
        if (mm < 1 || mm > 12)
            return false;

        int day = dd == 0 ? DateTime.DaysInMonth(year, mm) : dd;
        if (day < 1 || day > DateTime.DaysInMonth(year, mm))
            return false;

        date = new DateOnly(year, mm, day);
        return true;
    }
}
