using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Student2020ClientNoSSL_Kit.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FinalController : ControllerBase
    {
        private readonly IMemoryCache _cache;
        Random random = new Random();

        public FinalController(IMemoryCache memoryCache)
        {
            _cache = memoryCache;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { teamName = "Red team" });
        }


        [HttpPost]
        public IActionResult Post([FromBody] Playfield playfield)
        {
            //Place your code here

            //This is an example that returns a direction. You can use this to get started.
            // Direction d1 = ExampleRandomWithBoundsCheck(playfield);
            Direction d1 = GetNextDirection(playfield);
            return Ok(d1);
        }


        public Direction GetNextDirection(Playfield pf)
        {
            
            Cell head = pf.mytrain[0];
            List<Direction> dir;
            List<Direction> dirTemp;

            if (pf.energyLevel < 65 && pf.energyLevel > 20)
            {
                pf.energyStations.RemoveAll(es => !es.IsActive); // Remove all the stations that needs more than 5 cycles to fullfill
                pf.energyStations.RemoveAll(es => pf.mytrain.Contains(es.point)); // Remove all the stations that are under the cargos snake
                pf.energyStations.Sort((c1, c2) => computeDistance(c1.point, head).CompareTo(computeDistance(c2.point, head))); // Sort by distance

                if (computeDistance(pf.energyStations[0].point, head) <= 2) // If there's a station within 2 cells
                {
                    dir = Dijkstra(head, pf.energyStations[0].point, pf);
                    return dir[0]; // direct return
                }
            }

            // Sort the cargos from the closest to the farthest
            //pf.cargoList.RemoveAll(c => pf.mytrain.Contains(c));
            pf.cargoList.Sort((c1,c2)=>computeDistance(c1,head).CompareTo(computeDistance(c2,head)));

            // Dijkstra pathfinder
            dir = Dijkstra(head, pf.cargoList[0], pf);
            
            int bestRouteLen = dir.Count;

            // Increase to test more cargos
            for(int i = 1; i < 3; i++)
            {
                dirTemp = Dijkstra(head, pf.cargoList[i], pf);
                if(dirTemp.Count < bestRouteLen)
                {
                    bestRouteLen = dirTemp.Count;
                    dir = dirTemp;
                }
            }

            if(bestRouteLen > pf.energyLevel-20)
            {
                pf.energyStations.RemoveAll(es => !es.IsActive); // Remove all the stations that needs more than 5 cycles to fullfill
                pf.energyStations.RemoveAll(es => pf.mytrain.Contains(es.point)); // Remove all the stations that are under the cargos snake
                pf.energyStations.Sort((c1, c2) => computeDistance(c1.point, head).CompareTo(computeDistance(c2.point, head))); // Sort by distance


                if (pf.energyStations.Count > 0)
                {
                    dir = Dijkstra(head, pf.energyStations[0].point, pf);
                }

                else
                {
                    Debug.WriteLine("Can't find any stations");
                    return dir[0]; // If we don't find any station we keep going toward the next cargo
                }
                bestRouteLen = dir.Count;

                for (int i = 1; i < 2; i++)
                {
                    dirTemp = Dijkstra(head, pf.energyStations[i].point, pf);

                    if (dirTemp.Count < bestRouteLen)
                    {
                        bestRouteLen = dirTemp.Count;
                        dir = dirTemp;
                    }
                }

            }

            return dir[0];
        }

        private List<Direction> Dijkstra(Cell position, Cell target, Playfield pf)
        {
            Cell cell = position;
            List<Cell> candidatesFromCell = new List<Cell>(); // Current neighbours
            List<Cell> cellsChecked = new List<Cell>(); // Overall checked
            List<Cell> cellsSearched = new List<Cell>(); // Cells from wich we already checked the neighbours

            Dictionary<Cell, Double> distances = new Dictionary<Cell, double>(); // Distances of cellsChecked
            Dictionary<Cell, Cell> paths = new Dictionary<Cell, Cell>();

            cellsChecked.Add(cell);
            distances.Add(cell, computeDistance(cell, target));

            candidatesFromCell = (GetNeighbCells(cell, cellsChecked, pf));

            // Sometimes it doesn't find the target and then process for a while. 1200 a quarter of the grid.
            while (!cell.Equals(target) && cellsChecked.Count < 1200)
            {

                foreach (Cell c in candidatesFromCell)
                {
                    distances.Add(c, computeDistance(c, target));
                    
                    paths.Add(c, cell); // We add the connection to the previous cell (they are neighbours any way)

                }

                cellsChecked.AddRange(candidatesFromCell);
                cellsSearched.AddRange(candidatesFromCell);

                cellsSearched.Remove(cell);
                // Sort all and get the closest from the 
                cellsSearched.Sort((c1, c2) => distances[c1].CompareTo(distances[c2]));

                if (cellsSearched.Count == 0) break;
                cell = cellsSearched[0];
                

                candidatesFromCell = GetNeighbCells(cell, cellsChecked, pf);

            }

            List<Direction> dir = AnalysisPaths(position, target, paths, distances);

            return dir;

        }


        private List<Direction> AnalysisPaths(Cell position, Cell target, Dictionary<Cell, Cell> paths, Dictionary<Cell, Double> distances)
        {

            List<Direction> directions = new List<Direction>();

            if (!paths.ContainsKey(target)) {
                Debug.WriteLine("Didn't reach the target");
                target = distances.OrderBy(x => x.Value).FirstOrDefault().Key; // Target gets the cell the least far from the actual target

                for(int i = 0; i < 100; i++) // Artificially increase the lenght to prioritize another path
                {
                    directions.Add(Direction.NOACTION);
                }
            }

            
            if (!paths.ContainsValue(position))
            {
                Debug.WriteLine("Error when searching for the origin");
                directions.Add(Direction.NOACTION);
                return directions;
            }

            Cell loopback = target;
            
            while (!loopback.Equals(position))
            {
                directions.Add(getDirFromCells(paths[loopback], loopback));
                loopback = paths[loopback];
            }

            directions.Reverse(); // Since we start from the end we reverse to get the instructions from the origin

            return directions;
        }

        private Direction getDirFromCells(Cell o, Cell d)
        {
            if (o.X > d.X) return Direction.LEFT;
            else if (o.X < d.X) return Direction.RIGHT;
            else if (o.Y > d.Y) return Direction.UP;
            else if (o.Y < d.Y) return Direction.DOWN;
            else
            {
                Debug.WriteLine("Can't get direction");
                return Direction.NOACTION;
            }
        }

        private double computeDistance(Cell c1, Cell c2)
        {
            // Pythagore
            return Math.Sqrt(Math.Pow(c1.X - c2.X, 2) + Math.Pow(c1.Y - c2.Y, 2));

        }

        private List<Cell> GetNeighbCells(Cell cell, List<Cell> cellsChecked, Playfield pf)
        {
            List<Cell> toBeAdded = new List<Cell>();
            int X = cell.X;
            int Y = cell.Y;

            Cell left = new Cell { X = X - 1, Y = Y };
            if (!cellsChecked.Contains(left) && !isMe(left, pf) && !isOutOfBound(left, pf) && !isWall(left, pf)) toBeAdded.Add(left);

            Cell right = new Cell { X = X + 1, Y = Y };
            if (!cellsChecked.Contains(right) && !isMe(right, pf) && !isOutOfBound(right, pf) && !isWall(right, pf)) toBeAdded.Add(right);

            Cell up = new Cell { X = X, Y = Y-1 };
            if (!cellsChecked.Contains(up) && !isMe(up, pf) && !isOutOfBound(up, pf) && !isWall(up, pf)) toBeAdded.Add(up);

            Cell down = new Cell { X = X, Y = Y+1 };
            if (!cellsChecked.Contains(down) && !isMe(down, pf) && !isOutOfBound(down, pf) && !isWall(down, pf)) toBeAdded.Add(down);

            return toBeAdded;
        }

        private bool isOutOfBound(Cell cell, Playfield pf)
        {
            int X = cell.X;
            int Y = cell.Y;

            if (X > pf.fieldWidth-1 || X < 0 || Y > pf.fieldHeight-1 || Y < 0) return true;
            else return false;
        }

        private bool isWall(Cell cell, Playfield pf)
        {
            return pf.obsticles.Contains(cell);
        }

        private bool isMe(Cell cell, Playfield pf)
        {
            return pf.mytrain.Contains(cell);
        }

        //EXAMPLE CODE TO USE STATE
        private Direction ExampleUsingState(Playfield playfield)
        {
            //Get my directions stored in state
            var x = GetStoredDirections();
            //If I don't have any directions stored, add 10 direction
            if (x.Count == 0)
            {
                for (int i = 0; i < 10; i++)
                {
                    DirectionsPush(playfield.currentDirection);
                }
            }
            //Return first in list (FIFO list)
            return DirectionsPop();
        }

        //Utility method: Get your stored list of directions as a list
        private List<Direction> GetStoredDirections()
        {
            if (!_cache.TryGetValue("StoredDirections", out List<Direction> dirs))
            {
                //dirs = JsonConvert.DeserializeObject<List<Direction>>(await System.IO.File.ReadAllTextAsync("countrylist.json"));
                dirs = new List<Direction>();
                MemoryCacheEntryOptions options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(25), // cache will expire in 25 seconds
                    SlidingExpiration = TimeSpan.FromSeconds(5) // caceh will expire if inactive for 5 seconds
                };
                _cache.Set("StoredDirections", dirs, options);
            }
            return dirs;
        }

        //Utility method: If you build up a list of directions, store then with this method
        private void SetStoredDirections(List<Direction> dirs)
        {
            _cache.Remove("StoredDirections");
            MemoryCacheEntryOptions options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60), // cache will expire in 25 seconds
                SlidingExpiration = TimeSpan.FromSeconds(60) // caceh will expire if inactive for 5 seconds
            };
            _cache.Set("StoredDirections", dirs, options);
        }

        //Utility method: If you want to use push and pop methods to store your list, use this to PUSH a new Direction to your list
        private void DirectionsPush(Direction dir)
        {
            var d = GetStoredDirections();
            d.Add(dir);
            SetStoredDirections(d);
        }

        //Utility method: If you want to use push and pop methods to store your list, use this to POP a new Direction to your list
        private Direction DirectionsPop()
        {
            var d = GetStoredDirections();
            if (d.Count > 0)
            {
                Direction direction = d.First();
                d.RemoveAt(0);
                SetStoredDirections(d);
                return direction;
            }
            return Direction.NOACTION;
        }

    }
}
