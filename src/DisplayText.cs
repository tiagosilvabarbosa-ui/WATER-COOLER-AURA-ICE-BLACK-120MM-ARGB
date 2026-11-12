using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RiseModerCooller
{
    public enum TextEffect { Scroll = 0, PingPong = 1, Blink = 2, LetterByLetter = 3 }

    /// <summary>
    /// O firmware do display só desenha números (00-99). Para "escrever" um nome,
    /// cada letra vira o dígito de 7 segmentos mais parecido com ela (estilo leet: A=4, E=3, S=5...).
    /// </summary>
    public static class DisplayText
    {
        // Letra -> dígito parecido em 7 segmentos. ' ' = sem equivalente (vira espaço).
        private static readonly Dictionary<char, char> Leet = new Dictionary<char, char>
        {
            {'A','4'}, {'B','8'}, {'C','0'}, {'D','0'}, {'E','3'}, {'F','7'}, {'G','6'}, {'H','4'},
            {'I','1'}, {'J','1'}, {'K','4'}, {'L','1'}, {'M','3'}, {'N','2'}, {'O','0'}, {'P','9'},
            {'Q','9'}, {'R','2'}, {'S','5'}, {'T','7'}, {'U','0'}, {'V','0'}, {'W','3'}, {'X','8'},
            {'Y','4'}, {'Z','2'}
        };

        public static char ToGlyph(char c)
        {
            if (c >= '0' && c <= '9') return c;
            c = char.ToUpperInvariant(RemoveAccent(c));
            char g;
            return Leet.TryGetValue(c, out g) ? g : ' ';
        }

        public static string ToGlyphs(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder sb = new StringBuilder(text.Length);
            foreach (char c in text) sb.Append(ToGlyph(c));
            return sb.ToString();
        }

        private static char RemoveAccent(char c)
        {
            string s = c.ToString().Normalize(NormalizationForm.FormD);
            foreach (char ch in s)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) return ch;
            return c;
        }

        /// <summary>Gera a sequência de quadros de 2 caracteres (cada quadro = o que aparece no display).</summary>
        public static List<string> BuildFrames(string text, TextEffect effect)
        {
            // O display SEMPRE mostra 2 dígitos (não existe "apagado": 0 vira "00", 7 vira "07").
            // Por isso nenhum quadro usa espaço; espaços/pontuação do texto são removidos.
            string g = ToGlyphs(text ?? "").Replace(" ", "");
            List<string> frames = new List<string>();
            if (g.Length == 0) { frames.Add("00"); return frames; }
            if (g.Length == 1) g = g + g;

            List<string> windows = new List<string>();
            for (int i = 0; i <= g.Length - 2; i++) windows.Add(g.Substring(i, 2));

            switch (effect)
            {
                case TextEffect.Scroll:
                    // segura o começo e o fim um pouco para marcar onde o nome começa/termina
                    frames.Add(windows[0]); frames.Add(windows[0]);
                    frames.AddRange(windows);
                    frames.Add(windows[windows.Count - 1]); frames.Add(windows[windows.Count - 1]);
                    break;
                case TextEffect.PingPong:
                    frames.Add(windows[0]);
                    frames.AddRange(windows);
                    frames.Add(windows[windows.Count - 1]);
                    for (int i = windows.Count - 2; i >= 1; i--) frames.Add(windows[i]);
                    break;
                case TextEffect.Blink: // "pares": mostra 2 letras por vez, pulando de 2 em 2
                    for (int i = 0; i < g.Length; i += 2)
                    {
                        string pair = i + 2 <= g.Length ? g.Substring(i, 2) : g.Substring(g.Length - 2, 2);
                        frames.Add(pair); frames.Add(pair); frames.Add(pair);
                    }
                    break;
                case TextEffect.LetterByLetter: // cada letra dobrada: A -> "44"
                    foreach (char c in g.Length == 2 && g[0] == g[1] ? g.Substring(0, 1) : g)
                    {
                        string s = new string(c, 2);
                        frames.Add(s); frames.Add(s);
                    }
                    break;
            }
            if (frames.Count == 0) frames.Add(windows[0]);
            return frames;
        }

        /// <summary>Converte um quadro de 2 caracteres no byte enviado ao cooler.</summary>
        public static byte FrameToValue(string frame)
        {
            if (frame == null || frame.Length < 2) frame = (frame ?? "").PadLeft(2, '0');
            int l = char.IsDigit(frame[0]) ? frame[0] - '0' : 0;   // não há "apagado": espaço vira 0
            int r = char.IsDigit(frame[1]) ? frame[1] - '0' : 0;
            return (byte)(l * 10 + r);
        }

        /// <summary>Como o display realmente mostra um valor: sempre 2 dígitos ("07", "00").</summary>
        public static string ValueToFrame(byte v)
        {
            return (v % 100).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
