namespace PlantUmlClassDiagramGenerator.Library.ClassDiagramGenerator;

public class TypeDeterminator
{
    public static bool CheckIfThereAnyAngleBrackets(string s)
    {
        return s.Contains("<");
    }

    public static string GetWordBeforeAngleBracket(string s)
    {
        return s.Split('<')[0];
    }
    
    public static string GetWordInAngleBrackets(string s)
    {
        return s.Substring(s.IndexOf('<') + 1, s.LastIndexOf('>') - s.IndexOf('<') - 1);
    }
}