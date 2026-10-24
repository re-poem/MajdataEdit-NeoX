namespace MajdataEdit_NeoX.Services;

public interface IEditorService
{
    string GetSelectedText();
    void SetSelectedText(string value);
}
