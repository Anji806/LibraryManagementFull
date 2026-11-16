namespace LibraryManagement.DTOs.Dictionary;

public class DictionaryEntryDto
{
    public string Word { get; set; } = string.Empty;

    public string? Phonetic { get; set; }

    public string? AudioUrl { get; set; }

    public List<DictionaryMeaningDto> Meanings { get; set; } = new();
}

public class DictionaryMeaningDto
{
    public string PartOfSpeech { get; set; } = string.Empty;

    public List<DictionaryDefinitionDto> Definitions { get; set; } = new();
}

public class DictionaryDefinitionDto
{
    public string Definition { get; set; } = string.Empty;

    public string? Example { get; set; }
}
