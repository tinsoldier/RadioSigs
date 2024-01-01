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
    Func<IMyTerminalBlock, ICollection<MyDetectedEntityInfo>, int> _getAllBroadcasters;
    Func<IMyTerminalBlock, ICollection<MyTuple<int, string, string, Vector3D, bool, Color>>> _getGpsList;
    Func<IMyTerminalBlock, string, string, Vector3D, Color, bool, bool, MyTuple<int, string, string, Vector3D, bool, Color>> _createGps;
    Func<IMyTerminalBlock, int, string, string, Vector3D, Color, bool, bool, MyTuple<int, string, string, Vector3D, bool, Color>> _updateGps;
    Func<IMyTerminalBlock, int, bool> _deleteGps;

    public bool Activate(IMyTerminalBlock pbBlock)
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

    public int GetAllBroadcasters(IMyTerminalBlock antennaBlock, ICollection<MyDetectedEntityInfo> output) => _getAllBroadcasters?.Invoke(antennaBlock, output) ?? 0;

    public ICollection<MyTuple<int, string, string, Vector3D, bool, Color>> GetGpsList(IMyTerminalBlock owner) => _getGpsList.Invoke(owner);
    public MyTuple<int, string, string, Vector3D, bool, Color> CreateGps(IMyTerminalBlock owner, string name, string description, Vector3D coords, Color color, bool showOnHud = true, bool temporary = false) => _createGps.Invoke(owner, name, description, coords, color, showOnHud, temporary);
    public MyTuple<int, string, string, Vector3D, bool, Color> UpdateGps(IMyTerminalBlock owner, int hash, string name, string description, Vector3D coords, Color color, bool showOnHud = true, bool temporary = false) => _updateGps.Invoke(owner, hash, name, description, coords, color, showOnHud, temporary);
    public bool DeleteGps(IMyTerminalBlock owner, int hash) => _deleteGps.Invoke(owner, hash);
}
```
