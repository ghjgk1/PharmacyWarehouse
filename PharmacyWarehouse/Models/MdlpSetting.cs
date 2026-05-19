namespace PharmacyWarehouse.Models;

public partial class MdlpSetting : ObservableObject
{
    private int _id;
    private bool _useMock;
    private string? _apiUrl;
    private string? _clientId;
    private string? _orgInn;
    private string? _orgName;
    private string? _subjectId;
    private int _simulatedDelaySeconds;
    private int _simulatedErrorRate;
    private int _maxRetries;

    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public bool UseMock
    {
        get => _useMock;
        set => SetProperty(ref _useMock, value);
    }

    public string? ApiUrl
    {
        get => _apiUrl;
        set => SetProperty(ref _apiUrl, value);
    }

    public string? ClientId
    {
        get => _clientId;
        set => SetProperty(ref _clientId, value);
    }

    public string? OrgInn
    {
        get => _orgInn;
        set => SetProperty(ref _orgInn, value);
    }

    public string? OrgName
    {
        get => _orgName;
        set => SetProperty(ref _orgName, value);
    }

    public string? SubjectId
    {
        get => _subjectId;
        set => SetProperty(ref _subjectId, value);
    }

    public int SimulatedDelaySeconds
    {
        get => _simulatedDelaySeconds;
        set => SetProperty(ref _simulatedDelaySeconds, value);
    }

    public int SimulatedErrorRate
    {
        get => _simulatedErrorRate;
        set => SetProperty(ref _simulatedErrorRate, value);
    }

    public int MaxRetries
    {
        get => _maxRetries;
        set => SetProperty(ref _maxRetries, value);
    }
}
