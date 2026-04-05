namespace SlskDownBibliotecaImport;

public interface IBibliotecaImportUi
{
    void SetProgressMax(int max);
    void SetProgressValue(int value);
    void SetStatus(string text);
    void SetPerf(string text);
}
