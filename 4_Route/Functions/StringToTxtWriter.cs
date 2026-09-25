//Copyright Thomas Greshake 2026

namespace Brieffreund.Printer
{
    internal interface IStringToTxtWriter
    {
        public void Write(string name, string text);
    }
}

namespace Brieffreund.Printer.Functions
{
    internal class StringToTxtWriter : IStringToTxtWriter
    {
        internal StringToTxtWriter()
        {
        }

        public void Write(string fileName, string text)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            path = Path.Combine(path, fileName);

            bool success = true;

            try
            {
                using (StreamWriter sw = new StreamWriter(path))
                {
                    sw.WriteLine(text);
                }
            }
            catch (Exception)
            {
                success = false;
            }

            if (success)
            {
                Console.WriteLine("Eine Kopie der fertigen Route ist in der Text-Datei \"" + fileName + "\" im Dokument-Ordner zu finden.");
            }
        }
    }
}