﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using UnityEngine;
using UnityEngine.Networking;

namespace CLARTE.Net.LMS
{
	public class Client<T> where T : Enum
	{
		private struct Query
		{
			public UnityWebRequest request;
			public Action<string> onSuccess;
		}

		#region Members
		private const string urlKey = "LMS_url";
		private const string organizationKey = "LMS_organization";

		private Queue<Query> queue;
		#endregion

		#region Constructors
		public Client(string defaultUrl = "https://localhost")
		{
			queue = new Queue<Query>();

			if(!PlayerPrefs.HasKey(urlKey))
			{
				SetLmsUrl(defaultUrl);
			}
		}
		#endregion

		#region Getters / Setters
		public Entities.User User { get; private set; }

		public bool LoggedIn
		{
			get
			{
				return User != null && User.token != null;
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
			User = null;
		}

		public void Login(string username, string password, Action<bool> completion_callback = null)
		{
			HttpGet<Entities.User>("login", x =>
			{
				User = x;

				completion_callback?.Invoke(LoggedIn);
			}, new Dictionary<string, string>
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
				{ "name", application.title },
			});
		}

		public void RegisterModule(Content.Module module)
		{
			HttpGet<Entities.Module>("lms/module/register", null, new Dictionary<string, string>
			{
				{ "application", module.application.Guid.ToString() },
				{ "guid", module.Guid.ToString() },
				{ "name", module.title },
			});
		}

		public void RegisterExercise(Content.Exercise<T> exercise)
		{
			HttpGet<Entities.Exercise>("lms/exercise/register", null, new Dictionary<string, string>
			{
				{ "module", exercise.module.Guid.ToString() },
				{ "guid", exercise.Guid.ToString() },
				{ "name", exercise.title },
				{ "level", ((long)(object) exercise.level).ToString() },
			});
		}

		public void AddExerciseRecord(Content.Exercise<T> exercise, TimeSpan duration, bool success, float grade, uint nb_challenges_validated, byte[] debrief_data = null)
		{
			Dictionary<string, string> parameters = new Dictionary<string, string>
			{
				{ "exercise", exercise.Guid.ToString() },
				{ "duration", ((uint) duration.TotalSeconds).ToString() },
				{ "success", success.ToString() },
				{ "grade", string.Format(CultureInfo.InvariantCulture, "{0:N}", grade) },
				{ "nb_challenges_validated", nb_challenges_validated.ToString() },
			};

			if(debrief_data != null)
			{
				parameters.Add("debrief_data", Convert.ToBase64String(debrief_data));
			}

			HttpGet<bool>("lms/exercise/record", null, parameters);
		}

		public void AddSpectatorRecord(Content.Exercise<T> exercise, TimeSpan duration)
		{
			HttpGet<bool>("lms/spectator/record", null, new Dictionary<string, string>
			{
				{ "exercise", exercise.Guid.ToString() },
				{ "duration", ((uint) duration.TotalSeconds).ToString() },
			});
		}

		public void AddDebriefRecord(Content.Exercise<T> exercise, TimeSpan duration)
		{
			HttpGet<bool>("lms/debrief/record", null, new Dictionary<string, string>
			{
				{ "exercise", exercise.Guid.ToString() },
				{ "duration", ((uint) duration.TotalSeconds).ToString() },
			});
		}

		public void GetApplicationSummary(Content.Application application, Action<Entities.ApplicationSummary> result_callback)
		{
			HttpGet("lms/application/summary", result_callback, new Dictionary<string, string>
			{
				{ "guid", application.Guid.ToString() },
			});
		}

		public void GetModuleSummary(Content.Module module, Action<Entities.ModuleSummary> result_callback)
		{
			HttpGet("lms/module/summary", result_callback, new Dictionary<string, string>
			{
				{ "guid", module.Guid.ToString() },
			});
		}

		public void GetExerciseSummary(Content.Exercise<T> exercise, Action<Entities.ExerciseSummary> result_callback)
		{
			HttpGet("lms/exercise/summary", result_callback, new Dictionary<string, string>
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

			if(User != null && User.token != null)
			{
				request.SetRequestHeader("Authorization", string.Format("Bearer {0}", User.token));
			}

			lock(queue)
			{
				queue.Enqueue(new Query
				{
					request = request,
					onSuccess = on_success
				});

				if(queue.Count == 1)
				{
					queue.Peek().request.SendWebRequest().completed += OnHttpGetCompleted;
				}
			}
		}

		protected void OnHttpGetCompleted(AsyncOperation op)
		{
			UnityWebRequestAsyncOperation operation = (UnityWebRequestAsyncOperation) op;

			UnityWebRequest request = operation.webRequest;

			Query query;

			lock(queue)
			{
				query = queue.Peek();

				if(query.request != request)
				{
					query.request = null;
					query.onSuccess = null;
				}
			}

			if(query.request != null)
			{
				if(request.isNetworkError)
				{
					Debug.LogErrorFormat("Error while processing '{0}' request: '{1}'", request.uri, request.error);
				}
				else
				{
					switch(request.responseCode)
					{
						case 200:
							query.onSuccess(operation.webRequest.downloadHandler.text);
							break;
						case 401:
							Debug.LogErrorFormat("Unauthorized access to '{0}'", request.uri);
							break;
						default:
							Debug.LogErrorFormat("Failed to access '{0}': status = {1}", request.uri, request.responseCode);
							break;
					}
				}

				request.downloadHandler.Dispose();
				request.Dispose();
			}
			else
			{
				Debug.LogError("The request completed does not match the pending request. Ignoring.");
			}

			lock(queue)
			{
				queue.Dequeue();

				if(queue.Count > 0)
				{
					queue.Peek().request.SendWebRequest().completed += OnHttpGetCompleted;
				}
			}
		}
		#endregion
	}
}
