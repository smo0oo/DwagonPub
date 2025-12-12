using UnityEngine;
using System.Collections.Generic;

public static class NavigationComputer
{
    // A struct to return the full trip data
    public struct TripData
    {
        public List<LocationNode> path;
        public int totalHours;
        public float estimatedFuelCost;
        public float estimatedRationsCost;
        public bool isValid;
    }

    /// <summary>
    /// Calculates the shortest path and estimated resource cost between two nodes.
    /// </summary>
    public static TripData CalculateTrip(LocationNode startNode, LocationNode endNode)
    {
        TripData data = new TripData();
        data.path = new List<LocationNode>();
        data.isValid = false;

        if (startNode == endNode || startNode == null || endNode == null) return data;

        // --- 1. BFS Pathfinding ---
        Queue<LocationNode> frontier = new Queue<LocationNode>();
        frontier.Enqueue(startNode);

        Dictionary<LocationNode, LocationNode> cameFrom = new Dictionary<LocationNode, LocationNode>();
        cameFrom[startNode] = null;

        bool found = false;

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            if (current == endNode)
            {
                found = true;
                break;
            }

            foreach (var connection in current.connections)
            {
                if (connection.destinationNode != null && !cameFrom.ContainsKey(connection.destinationNode))
                {
                    frontier.Enqueue(connection.destinationNode);
                    cameFrom[connection.destinationNode] = current;
                }
            }
        }

        if (!found) return data; // No path exists

        // --- 2. Reconstruct Path & Calculate Cost ---
        LocationNode step = endNode;
        while (step != null)
        {
            data.path.Add(step);
            step = cameFrom[step];
        }
        data.path.Reverse(); // Now it's Start -> End

        // Calculate Resources based on the path
        // We use default rates if the manager isn't found (for testing robustness)
        float fuelRate = 5f;
        float rationsRate = 2f;

        if (WagonResourceManager.instance != null)
        {
            fuelRate = WagonResourceManager.instance.fuelPerHour;
            rationsRate = WagonResourceManager.instance.rationsPerHour;
        }

        for (int i = 0; i < data.path.Count - 1; i++)
        {
            LocationNode a = data.path[i];
            LocationNode b = data.path[i + 1];

            // Find the connection to get the time
            foreach (var conn in a.connections)
            {
                if (conn.destinationNode == b)
                {
                    data.totalHours += conn.travelTimeHours;
                    break;
                }
            }
        }

        data.estimatedFuelCost = data.totalHours * fuelRate;
        data.estimatedRationsCost = data.totalHours * rationsRate;
        data.isValid = true;

        return data;
    }

    /// <summary>
    /// Returns the immediate next node to visit to get to the final destination.
    /// </summary>
    public static LocationNode GetNextStep(LocationNode current, LocationNode destination)
    {
        TripData trip = CalculateTrip(current, destination);
        if (trip.isValid && trip.path.Count > 1)
        {
            return trip.path[1]; // Index 0 is current, Index 1 is next
        }
        return null;
    }
}