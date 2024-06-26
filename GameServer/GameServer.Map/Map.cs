using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

using GameServer.Template;
using GameServer.Networking;

using GamePackets;
using GamePackets.Server;

namespace GameServer.Map;

public enum MineType : byte
{
    Mine,
    Mine2,
    Mine3
}

public class StoneMineInfo
{
    public MineType Mine;
    public int MineCount;
    public int MineFillCount;
    public DateTime RefillTime;

    public StoneMineInfo(MineType mine)
    {
        Mine = mine;
        MineCount = SEngine.Random.Next(200);
        RefillTime = SEngine.CurrentTime;
        MineFillCount = SEngine.Random.Next(80);
    }

    public void Refill()
    {
        MineCount = MineFillCount;
        RefillTime = SEngine.CurrentTime.AddMinutes(10.0);
    }
}

public sealed class Map
{
    public readonly int RouteID;
    public readonly GameMap MapInfo;

    public uint TotalFixedMonsters;
    public uint TotalSurvivingMonsters;
    public uint TotalAmountMonsterResurrected;
    public long TotalAmountMonsterDrops;
    public long TotalAmountGoldDrops;

    public bool ReplicaClosed;
    public byte ReplicaNode;
    public byte 九层副本节点;
    public GuardObject ReplicaGuards;
    public DateTime ProcessTime;
    public int 刷怪记录;
    public List<MonsterSpawn> Respawns;
    public HashSet<MapObject>[,] Cells;
    public StoneMineInfo[,] Mines;
    public Terrain Terrain;

    public MapArea ResurrectionArea;
    public MapArea RedNameArea;
    public MapArea TeleportationArea;
    public MapArea 攻沙快捷;
    public MapArea 传送区域沙左;
    public MapArea 传送区域沙右;
    public MapArea 传送区域皇宫;
    public MapArea DemonTower1Area;
    public MapArea DemonTower2Area;
    public MapArea DemonTower3Area;
    public MapArea DemonTower4Area;
    public MapArea DemonTower5Area;
    public MapArea DemonTower6Area;
    public MapArea DemonTower7Area;
    public MapArea DemonTower8Area;
    public MapArea DemonTower9Area;

    public HashSet<MapArea> Areas;
    public HashSet<MonsterSpawn> Spawns;
    public HashSet<MapGuard> Guards;
    public HashSet<PlayerObject> Players;
    public HashSet<PetObject> Pets;
    public HashSet<ItemObject> Items;
    public HashSet<MapObject> Objects;
    public Dictionary<byte, TeleportGate> TeleportGates;

    public byte MapStatus
    {
        get
        {
            if (Players.Count < 200)
                return 1;
            if (Players.Count < 500)
                return 2;
            return 3;
        }
    }

    public int MapID => MapInfo.MapID;
    public byte MinLevel => MapInfo.MinLevel;
    public byte LimitInstances => 1;
    public bool NoReconnect => MapInfo.NoReconnect;
    public byte NoReconnectMapID => MapInfo.NoReconnectMapID;
    public bool QuestMap => MapInfo.QuestMap;

    public Point StartPoint => Terrain.StartPoint;
    public Point EndPoint => Terrain.EndPoint;
    public Size MapSize => Terrain.MapSize;

    public HashSet<MapObject> this[Point point]
    {
        get
        {
            if (!ValidPoint(point))
                return new HashSet<MapObject>();

            var x = point.X - StartPoint.X;
            var y = point.Y - StartPoint.Y;

            if (Cells[x, y] == null)
                return Cells[x, y] = new HashSet<MapObject>();

            return Cells[x, y];
        }
    }

    public StoneMineInfo GetMine(Point point)
    {
        var x = point.X - StartPoint.X;
        var y = point.Y - StartPoint.Y;

        return Mines[x, y];
    }

    public Map(GameMap info, int 路线编号 = 1)
    {
        Areas = new HashSet<MapArea>();
        Spawns = new HashSet<MonsterSpawn>();
        Guards = new HashSet<MapGuard>();
        Players = new HashSet<PlayerObject>();
        Pets = new HashSet<PetObject>();
        Items = new HashSet<ItemObject>();
        Objects = new HashSet<MapObject>();
        TeleportGates = new Dictionary<byte, TeleportGate>();
        MapInfo = info;
        RouteID = 路线编号;
    }

    public void Process()
    {
        if (MapID != 80)
        {
            return;
        }
        if (Players.Count == 0)
        {
            ReplicaNode = 110;
        }
        else if (ReplicaNode <= 5)
        {
            if (SEngine.CurrentTime > ProcessTime)
            {
                BroadcastAnnouncement($"Monsters will refresh in {30 - ReplicaNode * 5} seconds, so be prepared!");
                ReplicaNode++;
                ProcessTime = SEngine.CurrentTime.AddSeconds(5.0);
            }
        }
        else if (ReplicaNode <= 5 + Respawns.Count)
        {
            if (ReplicaGuards.Dead)
            {
                ReplicaNode = 100;
                ProcessTime = SEngine.CurrentTime;
            }
            else if (SEngine.CurrentTime > ProcessTime)
            {
                int wave = ReplicaNode - 6;
                MonsterSpawn spawn = Respawns[wave];
                int num2 = 刷怪记录 >> 16;
                int num3 = 刷怪记录 & 0xFFFF;
                MonsterSpawnInfo 刷新信息 = spawn.Spawns[num2];
                if (刷怪记录 == 0)
                {
                    BroadcastAnnouncement($"The {wave + 1}th wave of monsters has appeared, please pay attention to defence.");
                }
                if (MonsterInfo.DataSheet.TryGetValue(刷新信息.MonsterName, out var moni))
                {
                    new MonsterObject(moni, this, int.MaxValue, new Point(995, 283), 1,
                        forbidResurrection: true, refreshNow: true).SurvivalTime = SEngine.CurrentTime.AddMinutes(30.0);
                }
                if (++num3 >= 刷新信息.SpawnCount)
                {
                    num2++;
                    num3 = 0;
                }
                if (num2 >= spawn.Spawns.Count)
                {
                    ReplicaNode++;
                    刷怪记录 = 0;
                    ProcessTime = SEngine.CurrentTime.AddSeconds(60.0);
                }
                else
                {
                    刷怪记录 = (num2 << 16) + num3;
                    ProcessTime = SEngine.CurrentTime.AddSeconds(2.0);
                }
            }
        }
        else if (ReplicaNode == 6 + Respawns.Count)
        {
            if (ReplicaGuards.Dead)
            {
                ReplicaNode = 100;
                ProcessTime = SEngine.CurrentTime;
            }
            else if (TotalSurvivingMonsters == 0)
            {
                BroadcastAnnouncement("All monsters have been defeated, and the halls will close in 30 seconds.");
                ReplicaNode = 110;
                ProcessTime = SEngine.CurrentTime.AddSeconds(30.0);
            }
        }
        else if (ReplicaNode <= 109)
        {
            if (SEngine.CurrentTime > ProcessTime)
            {
                BroadcastAnnouncement("The guards are dead. The hall is closing.");
                ReplicaNode += 2;
                ProcessTime = SEngine.CurrentTime.AddSeconds(2.0);
            }
        }
        else
        {
            if (ReplicaNode < 110 || !(SEngine.CurrentTime > ProcessTime))
                return;

            foreach (var player in Players)
            {
                if (player.Dead)
                    player.Resurrect();
                else
                    player.Teleport(MapManager.GetMap(player.RespawnMapIndex), AreaType.Resurrection);
            }
            foreach (var pet in Pets)
            {
                if (pet.Dead)
                    pet.Despawn();
                else
                    pet.PetRecall();
            }
            foreach (var item in Items)
                item.DestroyItem();
            foreach (var obj in Objects)
                obj.Despawn();
            ReplicaClosed = true;
        }
    }

    public void MakeStoneMines()
    {
        if (MapInfo.MineMap > 0)
        {
            Mines = new StoneMineInfo[MapSize.Width, MapSize.Height];
            for (var x = 0; x < MapSize.Width; x++)
            {
                for (var y = 0; y < MapSize.Height; y++)
                {
                    if ((x % 2 == 0) && (y % 2 == 0))
                    {
                        var m = MapInfo.MineMap switch
                        {
                            1 => MineType.Mine,
                            2 => MineType.Mine2,
                            3 => MineType.Mine3,
                            _ => MineType.Mine,
                        };

                        Mines[x, y] = new StoneMineInfo(m);
                    }
                }
            }
        }
    }

    public void AddObject(MapObject obj)
    {
        switch (obj.ObjectType)
        {
            case GameObjectType.Item: Items.Add(obj as ItemObject); break;
            case GameObjectType.Pet: Pets.Add(obj as PetObject); break;
            case GameObjectType.Player: Players.Add(obj as PlayerObject); break;
            default: Objects.Add(obj); break;
        }
    }

    public void RemoveObject(MapObject obj)
    {
        switch (obj.ObjectType)
        {
            case GameObjectType.Item: Items.Remove(obj as ItemObject); break;
            case GameObjectType.Pet: Pets.Remove(obj as PetObject); break;
            case GameObjectType.Player: Players.Remove(obj as PlayerObject); break;
            default: Objects.Remove(obj); break;
        }
    }

    public void BroadcastAnnouncement(string message)
    {
        if (Players.Count == 0)
            return;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(0);
        writer.Write(2415919107u);
        writer.Write(3);
        writer.Write(0);
        writer.Write(Encoding.UTF8.GetBytes(message + "\0"));
        byte[] buffer = ms.ToArray();
        foreach (var player in Players)
        {
            player.Enqueue(new SystemMessagePacket
            {
                Description = buffer
            });
        }
    }

    public override string ToString()
    {
        return MapInfo.ToString();
    }

    private MapArea GetArea(AreaType region)
    {
        return region switch
        {
            AreaType.Unknown => Areas.FirstOrDefault(),
            AreaType.Resurrection => ResurrectionArea,
            AreaType.攻沙快捷 => 攻沙快捷,
            AreaType.传送区域沙左 => 传送区域沙左,
            AreaType.传送区域沙右 => 传送区域沙右,
            AreaType.传送区域皇宫 => 传送区域皇宫,
            AreaType.RedName => RedNameArea,
            AreaType.Teleportation => TeleportationArea,
            AreaType.DemonTower1 => DemonTower1Area,
            AreaType.DemonTower2 => DemonTower2Area,
            AreaType.DemonTower3 => DemonTower3Area,
            AreaType.DemonTower4 => DemonTower4Area,
            AreaType.DemonTower5 => DemonTower5Area,
            AreaType.DemonTower6 => DemonTower6Area,
            AreaType.DemonTower7 => DemonTower7Area,
            AreaType.DemonTower8 => DemonTower8Area,
            AreaType.DemonTower9 => DemonTower9Area,
            AreaType.Random => Areas.FirstOrDefault(x => x.RegionType == AreaType.Random),
            _ => null,
        };
    }

    public Point GetRandomPosition(AreaType region)
    {
        var area = GetArea(region);
        if (area == null)
            return Point.Empty;

        var position = area.Coordinates;
        if (GetRandomXY(120, area.AreaRadius, ref position))
            return position;
        return Point.Empty;
    }

    public Point GetRandomTeleportPosition(Point point)
    {
        foreach (var area in Areas)
        {
            if (area.RegionType == AreaType.Random)
            {
                if (Compute.InRange(point, area.Coordinates, area.AreaRadius))
                {
                    var position = area.Coordinates;
                    if (GetRandomXY(120, area.AreaRadius, ref position))
                        return position;
                }
            }
        }
        return Point.Empty;
    }

    public bool IsInArea(Point point, AreaType region)
    {
        var area = GetArea(region);
        if (area != null)
        {
            if (Compute.InRange(point, area.Coordinates, area.AreaRadius))
                return true;
        }
        return false;
    }

    public bool GetRandomXY(int attempts, int distance, ref Point position)
    {
        for (var i = 0; i < attempts; i++)
        {
            if (CanMove(position))
                return true;

            GetRandomXY(distance, ref position);
        }

        return false;
    }

    public void GetRandomXY(int distance, ref Point position)
    {
        if (distance > 0)
        {
            position.X += SEngine.Random.Next(-distance, distance + 1);
            position.Y += SEngine.Random.Next(-distance, distance + 1);
        }
        else
        {
            int size, edge;

            size = (MapSize.Height + MapSize.Width) / 2;

            if (size < 250)
            {
                if (size < 50) edge = 2;
                else edge = 10;
            }
            else
                edge = SEngine.Random.Next(10, 30);

            position.X = StartPoint.X + (edge + SEngine.Random.Next(MapSize.Width - edge));
            position.Y = StartPoint.Y + (edge + SEngine.Random.Next(MapSize.Height - edge));
        }

        position.X = Math.Clamp(position.X, StartPoint.X, EndPoint.X);
        position.Y = Math.Clamp(position.Y, StartPoint.Y, EndPoint.Y);
    }

    public bool GetNearXY(int attempts, ref Point position)
    {
        int size, step, edge;

        size = (MapSize.Height + MapSize.Width) / 2;
        step = (size < 80) ? 3 : 6;

        if (size < 250)
        {
            if (size < 50) edge = 2;
            else edge = 10;
        }
        else
            edge = SEngine.Random.Next(10, 30);

        for (var i = 0; i < attempts; i++)
        {
            if (CanMove(position))
                return true;
            if (position.X < MapSize.Width - edge - 1) position.X += step;
            else
            {
                position.X = SEngine.Random.Next(MapSize.Width);
                if (position.Y < MapSize.Height - edge - 1) position.Y += step;
                else position.Y = SEngine.Random.Next(MapSize.Height);
            }
        }

        return false;
    }

    public bool ValidPoint(Point point)
    {
        return point.X >= StartPoint.X && point.Y >= StartPoint.Y && point.X < EndPoint.X && point.Y < EndPoint.Y;
    }

    public bool IsBlocking(Point point)
    {
        if (IsSafeArea(point))
            return false;

        foreach (var obj in this[point])
        {
            if (obj.Blocking)
                return true;
        }
        return false;
    }

    public int BlockingCount(Point point)
    {
        int count = 0;
        foreach (var obj in this[point])
        {
            if (obj.Blocking)
                count++;
        }
        return count;
    }

    public bool ValidTerrain(Point point)
    {
        if (ValidPoint(point))
            return (Terrain[point] & 0x10000000) == 268435456;
        return false;
    }

    public bool CanMove(Point point)
    {
        if (ValidTerrain(point))
            return !IsBlocking(point);
        return false;
    }

    public ushort GetTerrainHeight(Point point)
    {
        if (ValidPoint(point))
            return (ushort)((Terrain[point] & 0xFFFF) - 30);
        return 0;
    }

    public bool IsTerrainBlocked(Point start, Point end)
    {
        var distance = Compute.GetDistance(start, end);
        for (var i = 0; i <= distance; i++)
        {
            if (!ValidTerrain(Compute.GetFrontPosition(start, end, i)))
                return false;
        }
        return true;
    }

    public bool 自由区内(Point point)
    {
        if (ValidPoint(point))
            return (Terrain[point] & 0x20000) == 131072;
        return false;
    }

    public bool IsSafeArea(Point point)
    {
        if (ValidPoint(point))
        {
            if ((Terrain[point] & 0x40000) != 262144)
                return (Terrain[point] & 0x100000) == 1048576;
            return true;
        }
        return false;
    }

    public bool IsStallArea(Point point)
    {
        if (ValidPoint(point))
            return (Terrain[point] & 0x100000) == 1048576;
        return false;
    }

    public bool CanDrop(Point point, bool redName)
    {
        if (MapManager.SandCityStage >= 2 && (MapID == 152 || MapID == 178) && Settings.Default.沙巴克爆装备开关 == 0)
            return false;

        if (ValidPoint(point))
        {
            if ((Terrain[point] & 0x400000) == 4194304)
                return true;
            if ((Terrain[point] & 0x800000) == 8388608 && redName)
                return true;
        }
        return false;
    }
}
