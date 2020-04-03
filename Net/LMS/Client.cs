using System;
using System.Collections.Generic;
using System.Net.Http;
using UnityEngine;
using UnityEngine.Networking;

namespace CLARTE.Net.LMS
{
	public class Client : MonoBehaviour
	{
		#region Members
		private const string urlKey = "LMS_url";
		private const string organizationKey = "LMS_organization";

		public string defaultUrl = "https://localhost";

		private Entities.User user;
		#endregion

		#region Getters / Setters
		public bool LoggedIn
		{
			get
			{
				return user != null && user.Token != null;
			}
		}
		#endregion

		#region MonoBehaviour callbacks
		protected void Awake()
		{
			if(!PlayerPrefs.HasKey(urlKey))
			{
				SetLmsUrl(defaultUrl);
			}
		}
		#endregion

		#region Public API
		public string GetLmsUrl()
		{
			return PlayerPrefs.GetString(urlKey);
		}

		public void SetLmsUrl(string url)
		{
			PlayerPrefs.SetString(urlKey, url);
		}

		public string GetOrganization()
		{
			return PlayerPrefs.GetString(organizationKey);
		}

		public void SetOrganization(string organization)
		{
			PlayerPrefs.SetString(organizationKey, organization);
		}

		public void Logout()
		{
			user = null;
		}

		public void Login(string username, string password)
		{
			HttpGet<Entities.User>("login", x => user = x, new Dictionary<string, string>
			{
				{ "organization", PlayerPrefs.GetString(organizationKey) },
				{ "username", username },
				{ "password", password },
			});
		}

		public void RegisterApplication(Content.Application application)
		{
			HttpGet<Entities.Application>("lms/application/register", null, new Dictionary<string, string>
			{
				{ "guid", application.Guid.ToString() },
				{ "name", application.Name },
			});
		}

		public void RegisterModule(Content.Module module)
		{
			HttpGet<Entities.Module>("lms/module/register", null, new Dictionary<string, string>
			{
				{ "application", module.Application.Guid.ToString() },
				{ "guid", module.Guid.ToString() },
				{ "name", module.Name },
			});
		}

		public void RegisterExercise(Content.Exercise exercise)
		{
			HttpGet<Entities.Exercise>("lms/exercise/register", null, new Dictionary<string, string>
			{
				{ "module", exercise.Module.Guid.ToString() },
				{ "guid", exercise.Guid.ToString() },
				{ "name", exercise.Name },
				{ "level", exercise.Level.ToString() },
			});
		}

		public void AddExerciseRecord(Content.Exercise exercise, TimeSpan duration, bool success, float grade, uint nb_challenges_validated, byte[] debrief_data)
		{
			Dictionary<string, string> parameters = new Dictionary<string, string>
			{
				{ "exercise", exercise.Guid.ToString() },
				{ "duration", ((uint) duration.TotalSeconds).ToString() },
				{ "success", success.ToString() },
				{ "grade", grade.ToString() },
				{ "nb_challenges_validated", nb_challenges_validated.ToString() },
			};

			if(debrief_data != null)
			{
				parameters.Add("debrief_data", Convert.ToBase64String(debrief_data));
			}

			HttpGet<Entities.Exercise>("exercise/record", null, parameters);
		}

		public void AddSpectatorRecord(Content.Exercise exercise, TimeSpan duration)
		{
			HttpGet<Entities.Exercise>("spectator/record", null, new Dictionary<string, string>
			{
				{ "exercise", exercise.Guid.ToString() },
				{ "duration", ((uint) duration.TotalSeconds).ToString() },
			});
		}

		public void AddDebriefRecord(Content.Exercise exercise, TimeSpan duration)
		{
			HttpGet<Entities.Exercise>("debrief/record", null, new Dictionary<string, string>
			{
				{ "exercise", exercise.Guid.ToString() },
				{ "duration", ((uint) duration.TotalSeconds).ToString() },
			});
		}

		public void GetApplicationSummary(Content.Application application)
		{
			HttpGet<Entities.Exercise>("application/summary", null, new Dictionary<string, string>
			{
				{ "guid", application.Guid.ToString() },
			});
		}

		public void GetModuleSummary(Content.Module module)
		{
			HttpGet<Entities.Exercise>("module/summary", null, new Dictionary<string, string>
			{
				{ "guid", module.Guid.ToString() },
			});
		}

		public void GetExerciseSummary(Content.Exercise exercise)
		{
			HttpGet<Entities.Exercise>("exercise/summary", null, new Dictionary<string, string>
			{
				{ "guid", exercise.Guid.ToString() },
			});
		}
		#endregion

		#region Internal methods
		protected void HttpGet<T>(string endpoint, Action<T> on_success, IReadOnlyDictionary<string, string> parameters = null)
		{
			HttpGet(endpoint, json => on_success?.Invoke(JsonUtility.FromJson<T>(json)), parameters);
		}
		/*
		protected void HttpGet<T>(string endpoint, Action<T[]> on_success, IReadOnlyDictionary<string, string> parameters = null)
		{
			HttpGet(endpoint, json =>
			{
				on_success?.Invoke(JsonArray.FromJson<T>(json));
			}, parameters);
		}
		*/
		protected void HttpGet(string endpoint, Action<string> on_success, IReadOnlyDictionary<string, string> parameters = null)
		{
			UriBuilder builder = new UriBuilder(PlayerPrefs.GetString(urlKey));

			builder.Path = string.Format("api/{0}", endpoint);

			if(parameters != null)
			{
				using(FormUrlEncodedContent content = new FormUrlEncodedContent(parameters))
				{
					builder.Query = content.ReadAsStringAsync().Result;
				}
			}
			
			UnityWebRequest request = new UnityWebRequest(builder.Uri);

			request.downloadHandler = new DownloadHandlerBuffer();

			request.SetRequestHeader("Accept", "application/json");
			request.SetRequestHeader("Content-Type", "application/json");

			if(user != null && user.Token != null)
			{
				request.SetRequestHeader("Authorization", string.Format("Bearer {0}", user.Token));
			}

			request.SendWebRequest().completed += op =>
			{
				UnityWebRequestAsyncOperation operation = (UnityWebRequestAsyncOperation) op;

				UnityWebRequest req = operation.webRequest;

				if(req.isNetworkError)
				{
					Debug.LogErrorFormat("Error while processing '{0}' request: '{1}'", req.uri, req.error);
				}
				else
				{
					switch(req.responseCode)
					{
						case 200:
							on_success(operation.webRequest.downloadHandler.text);
							break;
						case 401:
							Debug.LogErrorFormat("Unauthorized access to '{0}'", req.uri);
							break;
						default:
							Debug.LogErrorFormat("Failed to access '{0}': status = {1}", req.uri, req.responseCode);
							break;
					}
				}

				req.downloadHandler.Dispose();
				req.Dispose();
			};
		}
		#endregion
	}
}
