using System;
using System.Linq;
using UnityEngine;

namespace MissionLens
{
    // Shapes of Resources/ML/mission.json (written by tools/build_unity_data.py).
    // Map coordinates are metres from the centre of the area: x east, z SOUTH (as in the data pipeline).
    [Serializable] public class Station
    {
        public string id, name, kind, note, why, waypoint;
        public float x, z, elev, slopeDeg, viewAz;
        public double lon, lat;
        public int roverSol, crismRank;
        public string[] sees, hidden;
        public bool hasSample;
        public string sampleName, sampleRock, sampleLocation, sampleDate, sampleSol;
        public float sampleDistM;
        public string panoSols, panoCredit;
        public float panoElMax, panoElMin;
        public float[] panoSky;   // mean colour of the mosaic's top rows, sRGB 0..1
    }
    [Serializable] public class Leg { public string from, to; public float distM, ascentM, slopeMaxDeg, pctOnTrack, estWalkMin; public float[] route; }
    [Serializable] public class MTask { public int index, plannedMin, startMin, endMin, leg, recordCheck; public string station, phase; public string[] checks; }
    [Serializable] public class Mission
    {
        public string title, subtitle, scenarioNote, objective, objectiveBasis, timelineBasis, recordNote;
        public int plannedTotalMin, gridW, gridH;
        public float routeTotalM, walkSpeed, sizeX, sizeZ, elevMin;
        public double lonMin, lonMax, latMin, latMax;
        public string crismObservation, medaStation, medaLine;
        public string[] crismBands;
        public float crismMPerPx, crismShiftM, crismGain;
        public Station[] stations;
        public Leg[] legs;
        public MTask[] tasks;
    }

    /// <summary>The mission and the terrain heights, loaded once from Resources.</summary>
    public class MLData
    {
        public Mission M;
        public float[] dtm;   // metres above the lowest point of the area, row 0 = north
        public int gw, gh;
        public float sx, sz;

        public static MLData Load()
        {
            var d = new MLData { M = JsonUtility.FromJson<Mission>(Resources.Load<TextAsset>("ML/mission").text) };
            d.gw = d.M.gridW; d.gh = d.M.gridH; d.sx = d.M.sizeX; d.sz = d.M.sizeZ;
            var bytes = Resources.Load<TextAsset>("ML/dtm").bytes;
            d.dtm = new float[d.gw * d.gh];
            Buffer.BlockCopy(bytes, 0, d.dtm, 0, d.dtm.Length * 4);
            return d;
        }

        public Station St(string id) => M.stations.First(s => s.id == id);

        /// <summary>Height above the area's lowest point at map position (x east, z south), bilinear.</summary>
        public float Elev(float x, float zSouth)
        {
            float fc = Mathf.Clamp((x / sx + .5f) * (gw - 1), 0, gw - 1.001f), fr = Mathf.Clamp((zSouth / sz + .5f) * (gh - 1), 0, gh - 1.001f);
            int c = (int)fc, r = (int)fr, i = r * gw + c; float u = fc - c, v = fr - r;
            return (dtm[i] * (1 - u) + dtm[i + 1] * u) * (1 - v) + (dtm[i + gw] * (1 - u) + dtm[i + gw + 1] * u) * v;
        }

        /// <summary>Ground slope in degrees at a map position, from heights 2 m to either side (our calculation).</summary>
        public float SlopeDeg(float x, float zSouth)
        {
            float gx = (Elev(x + 2, zSouth) - Elev(x - 2, zSouth)) / 4, gz = (Elev(x, zSouth + 2) - Elev(x, zSouth - 2)) / 4;
            return Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
        }

        /// <summary>Map position to the terrain's local frame in Unity: x east, y up, z NORTH.</summary>
        public Vector3 Local(float x, float zSouth, float lift = 0) => new Vector3(x, Elev(x, zSouth) + lift, -zSouth);

        public static float Bearing(float fromX, float fromZ, float toX, float toZ) => (Mathf.Atan2(toX - fromX, -(toZ - fromZ)) * Mathf.Rad2Deg + 360f) % 360f;
        public static string Hm(float min) => $"{Mathf.FloorToInt(min / 60):00}:{Mathf.RoundToInt(min % 60):00}";
        public static string Deg3(float a) => ((Mathf.RoundToInt(a) % 360 + 360) % 360).ToString("000");
    }
}
