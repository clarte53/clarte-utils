using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using UnityEngine;
using UnityEngine.Networking;
using CLARTE.Net.LMS.Entities;

namespace CLARTE.Net.LMS
{
	public class Client<T> where T : Enum
	{
		private struct Query
		{
			public UnityWebRequest request;
			public Action<string> onSuccess;
			public Action<string> onFailure;
		}

		private class Cache<U, V>
		{
			#region Members
			protected Dictionary<U, V> mapper;
			#endregion

			#region Constructors
			public Cache()
			{
				mapper = new Dictionary<U, V>();
			}
			#endregion

			#region Public methods
			public void Get(U id, Action<V> result_callback, Action<U, Action<V>> getter)
			{
				bool is_cached;
				V value;

				lock(mapper)
				{
					is_cached = mapper.TryGetValue(id, out value);
				}

				if(is_cached)
				{
					result_callback?.Invoke(value);
				}
				else
				{
					getter(id, result =>
					{
						if(result != null)
						{
							lock(mapper)
							{
								mapper[id] = result;
							}
						}

						result_callback?.Invoke(result);
					});
				}
			}
			#endregion
		}

		#region Members
		private const string urlKey = "LMS_url";
		private const string organizationKey = "LMS_organization";

		private string defaultUrl;
		private Queue<Query> queue;
		private Cache<string, User> userNameCache;
		private Cache<long, User> userIdCache;
		private Cache<Content.Application, Entities.Application> applicationGuidCache;
		private Cache<long, Entities.Application> applicationIdCache;
		private Cache<Content.Module, Module> moduleGuidCache;
		private Cache<long, Module> moduleIdCache;
		private Cache<Content.Exercise<T>, Exercise> exerciseGuidCache;
		private Cache<long, Exercise> exerciseIdCache;
		#endregion

		#region Constructors
		public Client(string default_url = "https://localhost")
		{
			defaultUrl = default_url;

			queue = new Queue<Query>();

			userNameCache = new Cache<string, User>();
			userIdCache = new Cache<long, User>();
			applicationGuidCache = new Cache<Content.Application, Entities.Application>();
			applicationIdCache = new Cache<long, Entities.Application>();
			moduleGuidCache = new Cache<Content.Module, Module>();
			moduleIdCache = new Cache<long, Module>();
			exerciseGuidCache = new Cache<Content.Exercise<T>, Exercise>();
			exerciseIdCache = new Cache<long, Exercise>();

			if(!PlayerPrefs.HasKey(urlKey))
			{
				SetLmsUrl(defaultUrl);
			}
		}
		#endregion

		#region Getters / Setters
		public User User { get; private set; }

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
			return PlayerPrefs.GetString(urlKey, defaultUrl);
		}

		public void SetLmsUrl(string url)
		{
			PlayerPrefs.SetString(urlKey, url);
			PlayerPrefs.Save();
		}

		public string GetOrganization()
		{
			return PlayerPrefs.GetString(organizationKey, null);
		}

		public void SetOrganization(string organization)
		{
			PlayerPrefs.SetString(organizationKey, organization);
			PlayerPrefs.Save();
		}

		public void Logout()
		{
			User = null;
		}

		public void Login(string username, string password, Action<bool> completion_callback = null)
		{
			HttpPost<User>("users/login", x =>
			{
				User = x;

				completion_callback?.Invoke(LoggedIn);
			}, error =>
			{
				Debug.LogError(error);

				completion_callback?.Invoke(false);
			}, new Dictionary<string, string>
			{
				{ "organization", PlayerPrefs.GetString(organizationKey) },
				{ "username", username },
				{ "password", password },
			});
		}

		public void GetUser(string username, Action<User> result_callback)
		{
			userNameCache.Get(username, result_callback, (n, c) => HttpGet(string.Format("users/{0}", n), c, m => ErrorHandler(m, c), null));
		}

		public void GetUser(long id, Action<User> result_callback)
		{
			userIdCache.Get(id, result_callback, (i, c) => HttpGet(string.Format("users/{0}", i), c, m => ErrorHandler(m, c), null));
		}

		public void GetUsersList(Action<List<User>> result_callback)
		{
			HttpGetArray("users/list", result_callback, m => ErrorHandler(m, result_callback), null);
		}

		public void GetGroupsList(Action<List<Group>> result_callback)
		{
			HttpGetArray("users/groups/list", result_callback, m => ErrorHandler(m, result_callback), null);
		}

		public void RegisterApplication(Content.Application application)
		{
			HttpGet<Entities.Application>(string.Format("lms/application/{0}/register", application.Guid), null, Debug.LogError, new Dictionary<string, string>
			{
				{ "name", application.title },
			});
		}

		public void RegisterModule(Content.Module module)
		{
			HttpGet<Module>(string.Format("lms/module/{0}/register", module.Guid), null, Debug.LogError, new Dictionary<string, string>
			{
				{ "application", module.application.Guid.ToString() },
				{ "name", module.title },
			});
		}

		public void RegisterExercise(Content.Exercise<T> exercise)
		{
			HttpGet<Exercise>(string.Format("lms/exercise/{0}/register", exercise.Guid), null, Debug.LogError, new Dictionary<string, string>
			{
				{ "module", exercise.module.Guid.ToString() },
				{ "name", exercise.title },
				{ "level", ((int)(object) exercise.level).ToString() },
			});
		}

		public void AddExerciseRecord(Content.Exercise<T> exercise, TimeSpan duration, bool success, float grade, uint nb_challenges_validated, byte[] debrief_data = null)
		{
			Dictionary<string, string> parameters = new Dictionary<string, string>
			{
				{ "duration", ((uint) duration.TotalSeconds).ToString() },
				{ "success", success.ToString() },
				{ "grade", string.Format(CultureInfo.InvariantCulture, "{0:N}", grade) },
				{ "nb_challenges_validated", nb_challenges_validated.ToString() },
			};

			if(debrief_data != null)
			{
				parameters.Add("debrief_data", Convert.ToBase64String(debrief_data));
			}

			HttpGet<bool>(string.Format("lms/exercise/{0}/record", exercise.Guid), null, Debug.LogError, parameters);
		}

		public void AddSpectatorRecord(Content.Exercise<T> exercise, TimeSpan duration)
		{
			HttpGet<bool>(string.Format("lms/exercise/{0}/spectator/record", exercise.Guid), null, Debug.LogError, new Dictionary<string, string>
			{
				{ "duration", ((uint) duration.TotalSeconds).ToString() },
			});
		}

		public void AddDebriefRecord(Content.Exercise<T> exercise, TimeSpan duration)
		{
			HttpGet<bool>(string.Format("lms/exercise/{0}/debrief/record", exercise.Guid), null, Debug.LogError, new Dictionary<string, string>
			{
				{ "duration", ((uint) duration.TotalSeconds).ToString() },
			});
		}

		public void GetApplication(Content.Application application, Action<Entities.Application> result_callback)
		{
			applicationGuidCache.Get(application, result_callback, (app, c) => HttpGet(string.Format("lms/application/{0}", app.Guid), c, m => ErrorHandler(m, c), null));
		}

		public void GetApplication(long id, Action<Entities.Application> result_callback)
		{
			applicationIdCache.Get(id, result_callback, (i, c) => HttpGet(string.Format("lms/application/{0}", i), c, m => ErrorHandler(m, c), null));
		}

		public void GetModule(Content.Module module, Action<Module> result_callback)
		{
			moduleGuidCache.Get(module, result_callback, (mod, c) => HttpGet(string.Format("lms/module/{0}", mod.Guid), c, m => ErrorHandler(m, c), null));
		}

		public void GetModule(long id, Action<Module> result_callback)
		{
			moduleIdCache.Get(id, result_callback, (i, c) => HttpGet(string.Format("lms/module/{0}", i), c, m => ErrorHandler(m, c), null));
		}

		public void GetExercise(Content.Exercise<T> exercise, Action<Exercise> result_callback)
		{
			exerciseGuidCache.Get(exercise, result_callback, (ex, c) => HttpGet(string.Format("lms/exercise/{0}", ex.Guid), c, m => ErrorHandler(m, c), null));
		}

		public void GetExercise(long id, Action<Exercise> result_callback)
		{
			exerciseIdCache.Get(id, result_callback, (i, c) => HttpGet(string.Format("lms/exercise/{0}", i), c, m => ErrorHandler(m, c), null));
		}

		public void GetApplicationSummary(Content.Application application, Action<ApplicationSummary> result_callback)
		{
			HttpGet(string.Format("lms/application/{0}/summary", application.Guid), result_callback, m => result_callback?.Invoke(null), null);
		}

		public void GetModuleSummary(Content.Module module, Action<ModuleSummary> result_callback)
		{
			HttpGet(string.Format("lms/module/{0}/summary", module.Guid), result_callback, m => result_callback?.Invoke(null), null);
		}

		public void GetExerciseSummary(Content.Exercise<T> exercise, Action<ExerciseSummary> result_callback)
		{
			HttpGet(string.Format("lms/exercise/{0}/summary", exercise.Guid), result_callback, m => result_callback?.Invoke(null), null);
		}

		public void GetExerciseHistory(ulong? max_count, ulong? offset, Action<ExerciseRecordsPage> result_callback)
		{
			Dictionary<string, string> parameters = new Dictionary<string, string>();

			if(max_count.HasValue)
			{
				parameters.Add("max_count", max_count.ToString());
			}

			if(offset.HasValue)
			{
				parameters.Add("offset", offset.ToString());
			}

			HttpGet("lms/exercise/history", result_callback, m => result_callback?.Invoke(null), parameters.Count > 0 ? parameters : null);
		}

		/// <summary>
		/// Get specified user history. Only trainers have sufficient rights to call this method.
		/// </summary>
		/// <param name="user">The user id to get history from, or -1 to get history for all users.</param>
		/// <param name="max_count">The maximum number of records to return.</param>
		/// <param name="offset">The offset of the first record to return, i.e. do not return the 'offset' last records.</param>
		/// <param name="result_callback">The function to call when the results become available.</param>
		public void GetExerciseHistory(long user, ulong? max_count, ulong? offset, Action<ExerciseRecordsPage> result_callback)
		{
			Dictionary<string, string> parameters = new Dictionary<string, string>();

			if(max_count.HasValue)
			{
				parameters.Add("max_count", max_count.ToString());
			}

			if(offset.HasValue)
			{
				parameters.Add("offset", offset.ToString());
			}

			HttpGet(string.Format("lms/exercise/history/{0}", user), result_callback, m => result_callback?.Invoke(null), parameters.Count > 0 ? parameters : null);
		}
		#endregion

		#region Internal methods
		protected void ErrorHandler<U>(string message, Action<U> result_callback) where U : class
		{
			Debug.LogError(message);

			result_callback?.Invoke(null);
		}

		protected void HttpGet<U>(string endpoint, Action<U> on_success, Action<string> on_failure, Dictionary<string, string> parameters = null)
		{
			HttpRequest(endpoint, HttpGetCreator, json => on_success?.Invoke(JsonUtility.FromJson<U>(json)), on_failure, parameters);
		}
		
		protected void HttpGetArray<U>(string endpoint, Action<List<U>> on_success, Action<string> on_failure, Dictionary<string, string> parameters = null)
		{
			HttpRequest(endpoint, HttpGetCreator, json => on_success?.Invoke(JsonArray.FromJson<U>(json)), on_failure, parameters);
		}

		protected void HttpPost<U>(string endpoint, Action<U> on_success, Action<string> on_failure, Dictionary<string, string> parameters = null)
		{
			HttpRequest(endpoint, HttpPostCreator, json => on_success?.Invoke(JsonUtility.FromJson<U>(json)), on_failure, parameters);
		}

		protected void HttpPostArray<U>(string endpoint, Action<List<U>> on_success, Action<string> on_failure, Dictionary<string, string> parameters = null)
		{
			HttpRequest(endpoint, HttpPostCreator, json => on_success?.Invoke(JsonArray.FromJson<U>(json)), on_failure, parameters);
		}

		protected UriBuilder UriCreator(string endpoint)
		{
			UriBuilder builder = new UriBuilder(PlayerPrefs.GetString(urlKey));

			builder.Path = string.Format("api/1/{0}", endpoint);

			return builder;
		}

		protected UnityWebRequest HttpGetCreator(string endpoint, Dictionary<string, string> parameters = null)
		{
			UriBuilder builder = UriCreator(endpoint);

			if(parameters != null)
			{
				using(FormUrlEncodedContent content = new FormUrlEncodedContent(parameters))
				{
					builder.Query = content.ReadAsStringAsync().Result;
				}
			}

			return UnityWebRequest.Get(builder.Uri);
		}

		protected UnityWebRequest HttpPostCreator(string endpoint, Dictionary<string, string> parameters = null)
		{
			return UnityWebRequest.Post(UriCreator(endpoint).Uri, parameters);
		}

		protected void HttpRequest(string endpoint, Func<string, Dictionary<string, string>, UnityWebRequest> creator, Action<string> on_success, Action<string> on_failure, Dictionary<string, string> parameters = null)
		{
			UnityWebRequest request = creator(endpoint, parameters);

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
					onSuccess = on_success,
					onFailure = on_failure
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
					query.onFailure?.Invoke(string.Format("Error while processing '{0}' request: '{1}'", request.uri.GetLeftPart(UriPartial.Path), request.error));
				}
				else
				{
					switch(request.responseCode)
					{
						case 200:
							query.onSuccess?.Invoke(operation.webRequest.downloadHandler.text);
							break;
						case 401:
							query.onFailure?.Invoke(string.Format("Unauthorized access to '{0}'", request.uri.GetLeftPart(UriPartial.Path)));
							break;
						default:
							query.onFailure?.Invoke(string.Format("Failed to access '{0}': status = {1}", request.uri.GetLeftPart(UriPartial.Path), request.responseCode));
							break;
					}
				}

				request.downloadHandler.Dispose();
				request.Dispose();
			}
			else
			{
				query.onFailure?.Invoke("The request completed does not match the pending request. Ignoring.");
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
