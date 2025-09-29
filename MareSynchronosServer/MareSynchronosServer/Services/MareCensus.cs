using MareSynchronos.API.Dto.User;
using Prometheus;
using System.Collections.Concurrent;

namespace MareSynchronosServer.Services;

public class MareCensus : IHostedService
{
    private record CensusEntry(ushort WorldId, short Race, short Subrace, short Gender)
    {
        public static CensusEntry FromDto(CensusDataDto dto)
        {
            return new CensusEntry(dto.WorldId, dto.RaceId, dto.TribeId, dto.Gender);
        }
    }

    private readonly ConcurrentDictionary<string, CensusEntry> _censusEntries = new(StringComparer.Ordinal);
    private readonly Dictionary<short, string> _dcs = new()
    {
        {101, "陆行鸟"},
        {102, "莫古力"},
        {103, "猫小胖"},
        {104, "豆豆柴"},
    };
    private readonly Dictionary<short, string> _gender = new()
    {
        {0, "男"},
        {1, "女"},
    };
    private readonly ILogger<MareCensus> _logger;
    private readonly Dictionary<short, string> _races = new()
    {
        {1,"人族"},
        {2,"精灵族"},
        {3,"拉拉菲尔族"},
        {4,"猫魅族"},
        {5,"鲁加族"},
        {6,"敖龙族"},
        {7,"硌狮族"},
        {8,"维埃拉族"},
    };
    private readonly Dictionary<short, string> _tribes = new()
    {
        {1,"中原之民"},
        {2,"高地之民"},
        {3,"森林之民"},
        {4,"黑影之民"},
        {5,"平原之民"},
        {6,"沙漠之民"},
        {7,"逐日之民"},
        {8,"护月之民"},
        {9,"北洋之民"},
        {10,"红焰之民"},
        {11,"晨曦之民"},
        {12,"暮晖之民"},
        {13,"掠日之民"},
        {14,"迷踪之民"},
        {15,"密林之民"},
        {16,"山林之民"},
    };
    private readonly Dictionary<ushort, (string, short)> _worlds = new()
    {
        {1175, ("晨曦王座", 101) },
        {1174, ("沃仙曦染", 101) },
        {1173, ("宇宙和音", 101) },
        {1167, ("红玉海", 101)   },
        {1060, ("萌芽池", 101)   },
        {1081, ("神意之地", 101) },
        {1044, ("幻影群岛", 101) },
        {1042, ("拉诺西亚", 101) },

        {1121, ("拂晓之间", 102) },
        {1166, ("龙巢神殿", 102) },
        {1113, ("旅人栈桥", 102) },
        {1076, ("白金幻象", 102) },
        {1176, ("梦羽宝境", 102) },
        {1171, ("神拳痕", 102)   },
        {1170, ("潮风亭", 102)   },
        {1172, ("白银乡", 102)   },

        {1179, ("琥珀原", 103)   },
        {1178, ("柔风海湾", 103) },
        {1177, ("海猫茶屋", 103) },
        {1169, ("延夏", 103)    },
        {1106, ("静语庄园", 103) },
        {1045, ("摩杜纳", 103)   },
        {1043, ("紫水栈桥", 103) },

        {1201, ("红茶川", 104)    },
        {1186, ("伊修加德", 104)  },
        {1180, ("太阳海岸", 104)  },
        {1183, ("银泪湖", 104)    },
        {1192, ("水晶塔", 104)    },
        {1202, ("萨雷安", 104)    },
        {1203, ("加雷马", 104)    },
        {1200, ("亚马乌罗提", 104)},
    };
    private readonly string _xivApiKey;
    private Gauge? _gauge;

    public MareCensus(ILogger<MareCensus> logger)
    {
        _logger = logger;
    }

    private bool Initialized => _gauge != null;

    public void ClearStatistics(string uid)
    {
        if (!Initialized) return;

        if (_censusEntries.Remove(uid, out var censusEntry))
        {
            ModifyGauge(censusEntry, increase: false);
        }
    }

    public void PublishStatistics(string uid, CensusDataDto? censusDataDto)
    {
        if (!Initialized || censusDataDto == null) return;

        var newEntry = CensusEntry.FromDto(censusDataDto);

        if (_censusEntries.TryGetValue(uid, out var entry))
        {
            if (entry != newEntry)
            {
                ModifyGauge(entry, increase: false);
                ModifyGauge(newEntry, increase: true);
                _censusEntries[uid] = newEntry;
            }
        }
        else
        {
            _censusEntries[uid] = newEntry;
            ModifyGauge(newEntry, increase: true);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        //if (string.IsNullOrEmpty(_xivApiKey)) return;

        _logger.LogInformation("Census:Init data");

        // _logger.LogInformation("Loading XIVAPI data");

        // using HttpClient client = new HttpClient();

        // Dictionary<ushort, short> worldDcs = new();

        // var dcs = await client.GetStringAsync("https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/WorldDCGroupType.csv", cancellationToken).ConfigureAwait(false);
        // // dc: https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/WorldDCGroupType.csv
        // // id, name, region

        // using var dcsReader = new StringReader(dcs);
        // using var dcsParser = new TextFieldParser(dcsReader);
        // dcsParser.Delimiters = [","];
        // // read 3 lines and discard
        // dcsParser.ReadLine(); dcsParser.ReadLine(); dcsParser.ReadLine();

        // while (!dcsParser.EndOfData)
        // {
        //     var fields = dcsParser.ReadFields();
        //     var id = short.Parse(fields[0], CultureInfo.InvariantCulture);
        //     var name = fields[1];
        //     if (string.IsNullOrEmpty(name) || id == 0) continue;
        //     _logger.LogInformation("DC: ID: {id}, Name: {name}", id, name);
        //     _dcs[id] = name;
        // }

        // var worlds = await client.GetStringAsync("https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/World.csv", cancellationToken).ConfigureAwait(false);
        // // world: https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/World.csv
        // // id, internalname, name, region, usertype, datacenter, ispublic

        // using var worldsReader = new StringReader(worlds);
        // using var worldsParser = new TextFieldParser(worldsReader);
        // worldsParser.Delimiters = [","];
        // // read 3 lines and discard
        // worldsParser.ReadLine(); worldsParser.ReadLine(); worldsParser.ReadLine();

        // while (!worldsParser.EndOfData)
        // {
        //     var fields = worldsParser.ReadFields();
        //     var id = ushort.Parse(fields[0], CultureInfo.InvariantCulture);
        //     var name = fields[1];
        //     var dc = short.Parse(fields[5], CultureInfo.InvariantCulture);
        //     var isPublic = bool.Parse(fields[6]);
        //     if (!_dcs.ContainsKey(dc) || !isPublic) continue;
        //     _worlds[id] = (name, dc);
        //     _logger.LogInformation("World: ID: {id}, Name: {name}, DC: {dc}", id, name, dc);
        // }

        // var races = await client.GetStringAsync("https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/Race.csv", cancellationToken).ConfigureAwait(false);
        // // race: https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/Race.csv
        // // id, masc name, fem name, other crap I don't care about

        // using var raceReader = new StringReader(races);
        // using var raceParser = new TextFieldParser(raceReader);
        // raceParser.Delimiters = [","];
        // // read 3 lines and discard
        // raceParser.ReadLine(); raceParser.ReadLine(); raceParser.ReadLine();

        // while (!raceParser.EndOfData)
        // {
        //     var fields = raceParser.ReadFields();
        //     var id = short.Parse(fields[0], CultureInfo.InvariantCulture);
        //     var name = fields[1];
        //     if (string.IsNullOrEmpty(name) || id == 0) continue;
        //     _races[id] = name;
        //     _logger.LogInformation("Race: ID: {id}, Name: {name}", id, name);
        // }

        // var tribe = await client.GetStringAsync("https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/Tribe.csv", cancellationToken).ConfigureAwait(false);
        // // tribe: https://raw.githubusercontent.com/xivapi/ffxiv-datamining/master/csv/Tribe.csv
        // // id masc name, fem name, other crap I don't care about

        // using var tribeReader = new StringReader(tribe);
        // using var tribeParser = new TextFieldParser(tribeReader);
        // tribeParser.Delimiters = [","];
        // // read 3 lines and discard
        // tribeParser.ReadLine(); tribeParser.ReadLine(); tribeParser.ReadLine();

        // while (!tribeParser.EndOfData)
        // {
        //     var fields = tribeParser.ReadFields();
        //     var id = short.Parse(fields[0], CultureInfo.InvariantCulture);
        //     var name = fields[1];
        //     if (string.IsNullOrEmpty(name) || id == 0) continue;
        //     _tribes[id] = name;
        //     _logger.LogInformation("Tribe: ID: {id}, Name: {name}", id, name);
        // }

        // _gender[0] = "Male";
        // _gender[1] = "Female";

        _gauge = Metrics.CreateGauge("mare_census", "mare informational census data", new[] { "dc", "world", "gender", "race", "subrace" });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void ModifyGauge(CensusEntry censusEntry, bool increase)
    {
        var subraceSuccess = _tribes.TryGetValue(censusEntry.Subrace, out var subrace);
        var raceSuccess = _races.TryGetValue(censusEntry.Race, out var race);
        var worldSuccess = _worlds.TryGetValue(censusEntry.WorldId, out var world);
        var genderSuccess = _gender.TryGetValue(censusEntry.Gender, out var gender);
        if (subraceSuccess && raceSuccess && worldSuccess && genderSuccess && _dcs.TryGetValue(world.Item2, out var dc))
        {
            if (increase)
                _gauge.WithLabels(dc, world.Item1, gender, race, subrace).Inc();
            else
                _gauge.WithLabels(dc, world.Item1, gender, race, subrace).Dec();
        }
    }
}