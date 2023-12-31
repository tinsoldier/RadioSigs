# RadioSigs

Example PB script
```
/*
 * R e a d m e
 * -----------
 * 
 * 
 */

RadioPbApi _radioPbApi;
bool _initialized = false;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100;

    _radioPbApi = new RadioPbApi();
    _radioPbApi.Activate(Me);
		Echo("Setup Success");
}

public void Main(string argument, UpdateType updateSource)
{
    if (!_initialized)
    {
        if (_radioPbApi == null)
        {
            _radioPbApi = new RadioPbApi();
            _radioPbApi.Activate(Me);
			
			Echo("Setup Success\n");
        }

        _initialized = true;


        return;
    }
	
    if (argument == "Test")
    {
  		List<IMyTerminalBlock> antennaBlocks = GetBlocksWithName<IMyRadioAntenna>("Antenna");
  		if(antennaBlocks.Count == 0)
  		{
  			Echo("no antennas");
  			return;
  		}
		
  		List<string> broadcasters = new List<string>();
  		var cnt = _radioPbApi.GetAllBroadcasters(antennaBlocks[0], broadcasters);
  		
  		if(cnt <= 0)
  		{
  			Echo("no broadcasters: " + cnt.ToString());
  			return;
  		}
		
        Echo(string.Join(", ", broadcasters));
    }	
}

List<IMyTerminalBlock> GetBlocksWithName<T>(string name) where T : class, IMyTerminalBlock
{
	List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
	GridTerminalSystem.SearchBlocksOfName(name, blocks);

	List<IMyTerminalBlock> filteredBlocks = new List<IMyTerminalBlock>();
	for (int i = 0; i < blocks.Count; i++)
	{
		IMyTerminalBlock block = blocks[i] as T;
		if (block != null)
		{
			filteredBlocks.Add(block);
		}
	}

	return filteredBlocks;
}

public class RadioPbApi
{
	private Func<IMyTerminalBlock, ICollection<string>, int> _getAllBroadcasters;
	
    public bool Activate(Sandbox.ModAPI.Ingame.IMyTerminalBlock pbBlock)
    {
        var dict = pbBlock.GetProperty("RadioPbAPI")?.As<IReadOnlyDictionary<string, Delegate>>().GetValue(pbBlock);
        return ApiAssign(dict);
    }

    public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
    {
        if (delegates == null) return false;

		AssignMethod(delegates, "GetAllBroadcasters", ref _getAllBroadcasters);

        return true;
    }

    private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
    {
        if (delegates == null)
        {
            field = null;
            return;
        }
        Delegate del;
        if (!delegates.TryGetValue(name, out del))
            throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");
        field = del as T;
        if (field == null)
            throw new Exception($"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
    }

	public int GetAllBroadcasters(IMyTerminalBlock antennaBlock, ICollection<string> output) => _getAllBroadcasters?.Invoke(antennaBlock, output) ?? 0;
}
```
