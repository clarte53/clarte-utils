using UnityEngine;

namespace CLARTE.Geometry.Collision
{
    /// <summary>
    /// Utility class containing extension methods for collision detection between BoxColliders using
    /// the Separating Axis Theorem (SAT) algorithm.
    /// </summary>
    public static class BoxSAT
    {
        /// <summary>
        /// Get the corners of the BoxCollider.
        /// </summary>
        /// <param name="box">The BoxCollider to get the corners form.</param>
        /// <param name="space">Defines if the result corners are defined in the local or world referential.</param>
        /// <returns>An array of 8 vectors, one for each of the box corners.</returns>
        public static Vector3[] GetCorners(this BoxCollider box, Space space = Space.Self)
        {
            Vector3 min = box.center - 0.5f * box.size;
            Vector3 max = box.center + 0.5f * box.size;

            Vector3[] corners = new Vector3[] {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            if (space == Space.World)
            {
                Transform t = box.transform;

                int count = corners.Length;

                for (int i = 0; i < count; i++)
                {
                    corners[i] = t.TransformPoint(corners[i]);
                }
            }

            return corners;
        }

        /// <summary>
        /// Get the corners of the BoxCollider.
        /// </summary>
        /// <param name="box">The BoxCollider to get the corners form.</param>
        /// <param name="space">Defines if the result corners are defined in the local or world referential.</param>
        /// <returns>An array of 4 vectors, one for each of the box corners.</returns>
        public static Vector3[] GetCorners2D(this BoxCollider box, Space space = Space.Self)
        {
            Vector3 min = box.center - 0.5f * box.size;
            Vector3 max = box.center + 0.5f * box.size;

            Vector3[] corners = new Vector3[] {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, max.y, min.z),
            };

            if (space == Space.World)
            {
                Transform t = box.transform;

                int count = corners.Length;

                for (int i = 0; i < count; i++)
                {
                    corners[i] = t.TransformPoint(corners[i]);
                }
            }

            return corners;
        }

        /// <summary>
        /// Detected collision between two OOBB using the Separating Axis Theorem (SAT) algorithm.
        /// </summary>
        /// <param name="a">The first box.</param>
        /// <param name="b">The second box.</param>
        /// <returns>True if the two boxes are colliding, false otherwise.</returns>
        public static bool Collision(this BoxCollider a, BoxCollider b)
        {
            Transform a_transform = a.transform;
            Transform b_transform = b.transform;

            Vector3 a_axis_x = a_transform.right;
            Vector3 a_axis_y = a_transform.up;
            Vector3 a_axis_z = a_transform.forward;
            Vector3 b_axis_x = b_transform.right;
            Vector3 b_axis_y = b_transform.up;
            Vector3 b_axis_z = b_transform.forward;

            // Get all the 15 test axis to use for Box-Box collision test using SAT
            Vector3[] axes = new Vector3[]
            {
                a_axis_x,
                a_axis_y,
                a_axis_z,
                b_axis_x,
                b_axis_y,
                b_axis_z,
                Vector3.Cross(a_axis_x, b_axis_x),
                Vector3.Cross(a_axis_x, b_axis_y),
                Vector3.Cross(a_axis_x, b_axis_z),
                Vector3.Cross(a_axis_y, b_axis_x),
                Vector3.Cross(a_axis_y, b_axis_y),
                Vector3.Cross(a_axis_y, b_axis_z),
                Vector3.Cross(a_axis_z, b_axis_x),
                Vector3.Cross(a_axis_z, b_axis_y),
                Vector3.Cross(a_axis_z, b_axis_z)
            };

            // Get the corners of each box
            Vector3[] a_corners = a.GetCorners(Space.World);
            Vector3[] b_corners = b.GetCorners(Space.World);

            int nb_axes = axes.Length;
            int nb_corners = a_corners.Length;

            // Test overlap on each axis
            for (int i = 0; i < nb_axes; i++)
            {
                Vector3 axis = axes[i];

                // Cross product = (0, 0, 0) => collinear base vectors
                // i.e. box alligned on some axis: we can safely skip the test on this degenerated axis
                if (axis != Vector3.zero)
                {
                    float a_proj_min = float.MaxValue;
                    float a_proj_max = float.MinValue;
                    float b_proj_min = float.MaxValue;
                    float b_proj_max = float.MinValue;

                    // Get min and max value of projected corners into current axis
                    for (int j = 0; j < nb_corners; j++)
                    {
                        float a_proj = Vector3.Dot(a_corners[j], axis);
                        float b_proj = Vector3.Dot(b_corners[j], axis);

                        if (a_proj < a_proj_min)
                        {
                            a_proj_min = a_proj;
                        }

                        if (a_proj > a_proj_max)
                        {
                            a_proj_max = a_proj;
                        }

                        if (b_proj < b_proj_min)
                        {
                            b_proj_min = b_proj;
                        }

                        if (b_proj > b_proj_max)
                        {
                            b_proj_max = b_proj;
                        }
                    }

                    bool overlap = false;

                    // Test if projections are overlaping on the current axis
                    if (a_proj_min == a_proj_max)
                    {
                        overlap = true;
                    }
                    else if (a_proj_min < b_proj_min)
                    {
                        if (a_proj_max >= b_proj_min)
                        {
                            overlap = true;
                        }
                    }
                    else
                    {
                        if (b_proj_max >= a_proj_min)
                        {
                            overlap = true;
                        }
                    }

                    // No collision if boxes does not overlap on at least one axis
                    if (!overlap)
                    {
                        return false;
                    }
                }
            }

            // All tested axis does overlap, therefore the boxes collide
            return true;
        }

        /// <summary>
		/// Detected collision between two OOBB using the Separating Axis Theorem (SAT) algorithm.
		/// </summary>
		/// <param name="a">The first box.</param>
		/// <param name="b">The second box.</param>
		/// <returns>True if the two boxes are colliding, false otherwise.</returns>
		public static bool Collision2D(this BoxCollider a, BoxCollider b)
        {
            Transform a_transform = a.transform;
            Transform b_transform = b.transform;

            Vector3 a_axis_x = (a_transform.right);
            Vector3 a_axis_y = (a_transform.up);
            Vector3 b_axis_x = (b_transform.right);
            Vector3 b_axis_y = (b_transform.up);

            // Get all the 4 test axis to use for Box-Box collision test using SAT
            Vector3[] axes = new Vector3[]
            {
                a_axis_x,
                a_axis_y,
                b_axis_x,
                b_axis_y,
            };

            // Get the corners of each box
            Vector3[] a_corners = a.GetCorners2D(Space.World);
            Vector3[] b_corners = b.GetCorners2D(Space.World);

            int nb_axes = axes.Length;
            int nb_corners = a_corners.Length;

            // Test overlap on each axis
            for (int i = 0; i < nb_axes; i++)
            {
                Vector3 axis = (axes[i]);

                // Cross product = (0, 0, 0) => collinear base vectors
                // i.e. box alligned on some axis: we can safely skip the test on this degenerated axis
                if (axis != Vector3.zero)
                {
                    float a_proj_min = float.MaxValue;
                    float a_proj_max = float.MinValue;
                    float b_proj_min = float.MaxValue;
                    float b_proj_max = float.MinValue;

                    // Get min and max value of projected corners into current axis
                    for (int j = 0; j < nb_corners; j++)
                    {
                        float a_proj = Vector3.Dot(Camera.main.WorldToScreenPoint(a_corners[j]), axis);
                        float b_proj = Vector3.Dot(Camera.main.WorldToScreenPoint(b_corners[j]), axis);

                        if (a_proj < a_proj_min)
                        {
                            a_proj_min = a_proj;
                        }

                        if (a_proj > a_proj_max)
                        {
                            a_proj_max = a_proj;
                        }

                        if (b_proj < b_proj_min)
                        {
                            b_proj_min = b_proj;
                        }

                        if (b_proj > b_proj_max)
                        {
                            b_proj_max = b_proj;
                        }
                    }

                    bool overlap = false;

                    // Test if projections are overlaping on the current axis
                    if (a_proj_min == a_proj_max)
                    {
                        overlap = true;
                    }
                    else if (a_proj_min < b_proj_min)
                    {
                        if (a_proj_max >= b_proj_min)
                        {
                            overlap = true;
                        }
                    }
                    else
                    {
                        if (b_proj_max >= a_proj_min)
                        {
                            overlap = true;
                        }
                    }

                    // No collision if boxes does not overlap on at least one axis
                    if (!overlap)
                    {
                        return false;
                    }
                }
            }

            // All tested axis does overlap, therefore the boxes collide
            return true;
        }

        /// <summary>
		/// Detected collision between two OOBB using the Separating Axis Theorem (SAT) algorithm.
		/// </summary>
		/// <param name="a">The first box.</param>
		/// <param name="b">The second box.</param>
		/// <returns>True if the two boxes are colliding, false otherwise.</returns>
		public static bool Collision2D(this BoxCollider a, BoxCollider b, out Vector3 direction, out float distance)
        {
            direction = Vector3.zero;
            distance = 0;

            bool overlap = Collision2D(a, b);

			if(overlap)
			{
                // Get the corners of each box
                Vector3[] a_corners = a.GetCorners2D(Space.World);
                Vector3[] b_corners = b.GetCorners2D(Space.World);

                float a_x_min = Camera.main.WorldToViewportPoint(a_corners[0]).x;
				float a_y_min = Camera.main.WorldToViewportPoint(a_corners[0]).y;
				float a_x_max = Camera.main.WorldToViewportPoint(a_corners[3]).x;
				float a_y_max = Camera.main.WorldToViewportPoint(a_corners[3]).y;

				float b_x_min = Camera.main.WorldToViewportPoint(b_corners[0]).x;
				float b_y_min = Camera.main.WorldToViewportPoint(b_corners[0]).y;
				float b_x_max = Camera.main.WorldToViewportPoint(b_corners[3]).x;
				float b_y_max = Camera.main.WorldToViewportPoint(b_corners[3]).y;

				Vector3 dirX = Camera.main.transform.right;
				Vector3 dirY = Camera.main.transform.up;
				float distX = Mathf.Abs(a_x_max - b_x_min + b_x_max - a_x_min);
				float distY = Mathf.Abs(a_y_max - b_y_min + b_y_max - a_y_min);

				direction = dirX * distX + dirY * distY;

				distance = direction.magnitude;

				direction = direction / distance;
			}
			
            return overlap;
        }
    }
}
