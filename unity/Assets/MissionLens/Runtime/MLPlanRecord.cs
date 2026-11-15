using System;
using System.IO;
using UnityEngine;

namespace MissionLens
{
    // The saved observation plan: what Application.persistentDataPath/ObservationPlans/<session>/observation_plan.json
    // holds. Plain fields and arrays only, so that JsonUtility reads and writes it; nothing of the interface is in it.
    // A plan is a rehearsal made on Earth from historical NASA data: every observation in it is one that is planned,
    // and none is recorded as collected. What was looked at (existing evidence) is kept apart from what is planned.
    // An observation still to be made, added from a photo: originCaptureId says which. status is always "planned".
    [Serializable] public class PlanItem
    {
        public string id, stationId, roleId, observationType, purposeId, status, originCaptureId, createdUtc;
        public string[] sourceIds, unresolvedIds;   // sourceIds: only evidence that was opened in this session
    }
    [Serializable] public class StationRole { public string stationId, roleId; }
    [Serializable] public class RelationshipPlan
    {
        public string[] stationIds; public string purposeId, status;
        public string[] sourceIds, unresolvedIds;
    }
    [Serializable] public class LosAssumptions { public float eyeHeightM, targetHeightM, toleranceM; public string gridDescription; }
    // A region pointed at in a panorama. The record is the image and the direction from the place the image was
    // taken; the map position is a second, approximate reading (the pointing direction met with the terrain model).
    [Serializable] public class PhotoObservation
    {
        public string stationId, photoId, photoSolRange; public float azDeg, elDeg; public string coordinateType, reasonId;
        public bool hasApproximateMap; public float approximateMapX, approximateMapZ;
    }
    // A view of a historical panorama framed by the person: where it looks from the panorama's centre and how wide.
    [Serializable] public class CaptureRecord
    {
        public string id, stationId, panoramaSourceId, sourceSolRange;
        public float azDeg, elDeg, horizontalFovDeg, verticalFovDeg;
        // purposeId: what the view was for when it was taken (purposeSource: inherited from the task selected then,
        // sourceTaskId, or chosen by the person). needsReview: set aside to look at again; it says nothing else.
        public string projection, imageRelativePath, purposeId, purposeSource, sourceTaskId, planItemId, createdUtc, kind;
        public bool needsReview;
    }
    [Serializable] public class PlanRecord
    {
        public int schemaVersion = 1;
        // buildId: the stamp of the device build that wrote the plan; in the Editor, where there is no build, it says so.
        // runtime: the platform and Unity version it ran on. inputHash: of the mission data (Resources/ML/mission.json).
        public string note, sessionId, createdUtc, savedUtc, buildId, runtime, inputHash, goalId, scenarioId;
        public string[] initialPlanItemIds = new string[0], savedPlanItemIds = new string[0];
        public StationRole[] stationRoles = new StationRole[0];
        public PlanItem[] planItems = new PlanItem[0];
        public RelationshipPlan relationshipPlan;   // stationIds empty: not in the plan
        public string[] viewedEvidenceIds = new string[0], visitedStationIds = new string[0];
        public LosAssumptions losAssumptions;
        public PhotoObservation[] photoObservations = new PhotoObservation[0];
        public CaptureRecord[] captures = new CaptureRecord[0];
    }

    /// <summary>Where observation plans are kept, and reading and writing them. Errors are the caller's to show.</summary>
    public static class MLPlanStore
    {
        public const string FileName = "observation_plan.json", Latest = "latest.txt";
        /// <summary>For checks from the Editor: a folder to use instead (one that cannot be written shows the failure path).</summary>
        public static string rootOverride;
        public static string Root => rootOverride ?? Path.Combine(Application.persistentDataPath, "ObservationPlans");
        public static string Dir(string sessionId) => Path.Combine(Root, sessionId);

        /// <summary>Writes the plan beside its images, then points "latest" at it. Throws if anything fails.</summary>
        public static string Save(PlanRecord r)
        {
            string dir = Dir(r.sessionId); Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, FileName), tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(r, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            var back = JsonUtility.FromJson<PlanRecord>(File.ReadAllText(path));   // what is reported as saved is what can be read back
            if (back == null || back.sessionId != r.sessionId || back.planItems.Length != r.planItems.Length || back.captures.Length != r.captures.Length) throw new IOException("the saved plan did not read back");
            File.WriteAllText(Path.Combine(Root, Latest), r.sessionId);
            return path;
        }

        /// <summary>The plan saved last, or null if there is none (or it cannot be read).</summary>
        public static PlanRecord LoadLatest()
        {
            try
            {
                string latest = Path.Combine(Root, Latest); if (!File.Exists(latest)) return null;
                string path = Path.Combine(Dir(File.ReadAllText(latest).Trim()), FileName); if (!File.Exists(path)) return null;
                return JsonUtility.FromJson<PlanRecord>(File.ReadAllText(path));
            }
            catch (Exception e) { Debug.LogWarning("[ML] saved observation plan not read: " + e.Message); return null; }
        }
    }
}
