using Hvylia.Api.Models;

namespace Hvylia.Api.Services;

public sealed class DataStore
{
    private readonly object _lock = new();
    public Dataset? Dataset { get; private set; }
    public HiddenWeekRequest LastHiddenWeekRequest { get; private set; } = new();

    public void SetDataset(Dataset ds) { lock (_lock) Dataset = ds; }

    public void SetEquipment(List<EquipmentItem> eq)
    {
        lock (_lock) { Dataset ??= new Dataset(); Dataset.Equipment = eq; }
    }

    public void SetHiddenWeekRequest(HiddenWeekRequest r) { lock (_lock) LastHiddenWeekRequest = r; }
}
