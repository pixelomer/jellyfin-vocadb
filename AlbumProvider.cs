using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Collections.Specialized;
using System.Web;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Extensions;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.VocaDB {
	public class AlbumExternalId : IExternalId {
		public string ProviderName => "VocaDB";
		public string Key => "vocadb-album";
		public ExternalIdMediaType? Type => ExternalIdMediaType.Album;
		public string UrlFormatString => AlbumProvider.UrlFormatString;
		public bool Supports(IHasProviderIds item) => item is MusicAlbum;
	}

	public class AlbumProvider : IRemoteMetadataProvider<MusicAlbum, AlbumInfo> {
		internal class ImageObject {
			public string mime { get; set; }
			public string urlOriginal { get; set; }
			public string urlThumb { get; set; }
			public string urlSmallThumb { get; set; }
			public string urlTinyThumb { get; set; }
		}
		internal class SongObject {
			public ImageObject mainPicture { get; set; }
			public string artistString { get; set; }
			public string defaultName { get; set; }
			public DateTimeOffset createDate { get; set; }
			public int id { get; set; }
			public string name { get; set; }
			public ReleaseDate releaseDate { get; set; }
		}
		internal class ReleaseDate {
			public int day { get; set; }
			public string formatted { get; set; }
			public bool isEmpty { get; set; }
			public int month { get; set; }
			public int year { get; set; }
		}
	
		internal class AlbumResponse {
			internal class Track {
				public int discNumber { get; set; }
				public int id { get; set; }
				public string name { get; set; }
				public SongObject song { get; set; }
				public int trackNumber { get; set; }
			}
			public List<Track> tracks { get; set; }
			public string artistString { get; set; }
			public ImageObject mainPicture { get; set; }
			public DateTimeOffset createDate { get; set; }
			public string defaultName { get; set; }
			public string defaultNameLanguage { get; set; }
			public string discType { get; set; }
			public int id { get; set; }
			public string name { get; set; }
			public float ratingAverage { get; set; }
			public int ratingCount { get; set; }
			public ReleaseDate releaseDate { get; set; }
			public string status { get; set; }
		}

		internal class AlbumSearchResponse {
			public string term { get; set; }
			public List<SongObject> items { get; set; }
		}

		internal string ToQueryString(NameValueCollection nvc) {
			// https://stackoverflow.com/questions/829080/829138#829138
			var array = (
				from key in nvc.AllKeys
					from value in nvc.GetValues(key)
						select string.Format(
							"{0}={1}",
							HttpUtility.UrlEncode(key),
							HttpUtility.UrlEncode(value))
				).ToArray();
			return "?" + string.Join("&", array);
		}
		public const string UrlFormatString = "https://vocadb.net/api/albums/{0}";
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly ILogger<AlbumProvider> _logger;
		public AlbumProvider(IHttpClientFactory httpClientFactory, ILogger<AlbumProvider> logger) {
			_httpClientFactory = httpClientFactory;
			_logger = logger;
		}
		public string Name => "VocaDB";
		internal async Task<HttpResponseMessage> GetResponse(string url) {
			HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
			return (await _httpClientFactory
				.CreateClient(NamedClient.Default)
				.SendAsync(request)
				.ConfigureAwait(false))
				.EnsureSuccessStatusCode();
		}
		public async Task<MetadataResult<MusicAlbum>> GetMetadata(AlbumInfo info, CancellationToken cancellationToken) {
			string providerId = info.GetProviderId(Plugin.ProviderId);
			MetadataResult<MusicAlbum> result = new MetadataResult<MusicAlbum> {
				Item = new MusicAlbum()
			};
			if (!string.IsNullOrWhiteSpace(providerId)) {
				// Prepare query
				NameValueCollection query = new NameValueCollection();
				query.Set("fields", "Artists,MainPicture,Tracks");
				query.Set("songFields", "MainPicture,ThumbUrl,Artists,Tags");
				query.Set("Language", "Default");
				string url = string.Format(UrlFormatString, HttpUtility.UrlEncode(providerId) + ToQueryString(query));

				// Parse response
				HttpResponseMessage response = await GetResponse(url);
				AlbumResponse albumResponse = await JsonSerializer.DeserializeAsync<AlbumResponse>(response.Content.ReadAsStream());
				if (albumResponse.name != null) {
					result.HasMetadata = true;
					result.Item.Name = albumResponse.name;
					/*PersonInfo artist = new PersonInfo {
						Name = albumResponse.artistString
					};
					artist.SetProviderId(Plugin.ProviderId, albumResponse.arti)
					result.AddPerson();*/
					result.ResultLanguage = albumResponse.defaultNameLanguage;
					result.Item.ProductionYear = albumResponse.releaseDate.year;
					result.Item.CommunityRating = albumResponse.ratingAverage;
					ItemImageInfo albumImage = new ItemImageInfo() {
						Path = albumResponse.mainPicture.urlOriginal,
						Type = ImageType.Primary
					};
					result.Item.SetImage(albumImage, 0);
					result.Item.AddGenre("Vocaloid");
					result.Item.SetProviderId(Plugin.ProviderId, providerId);
					/*foreach (var trackInResponse in albumResponse.tracks) {
						Audio audio = new Audio();
						audio.SetProviderId(Plugin.ProviderId, trackInResponse.id.ToString());
						audio.Name = trackInResponse.name;
						audio.IndexNumber = trackInResponse.trackNumber;
						audio.ParentIndexNumber = trackInResponse.discNumber;
						result.Item.AddChild(audio, cancellationToken);
					}*/
				}
			}
			return result;
		}

		public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(AlbumInfo searchInfo, CancellationToken cancellationToken) {
			List<RemoteSearchResult> results = new List<RemoteSearchResult>();

			// If the search query is empty, abort
			if (string.IsNullOrWhiteSpace(searchInfo.Name)) {
				return results; // Empty
			}

			// Prepare query
			NameValueCollection query = new NameValueCollection();
			query.Set("start", "0");
			query.Set("getTotalCount", "true");
			query.Set("maxResults", "10");
			query.Set("query", searchInfo.Name);
			//query.Set("fields", "AdditionalNames,MainPicture,ReleaseEvent");
			query.Set("fields", "MainPicture");
			query.Set("lang", "Default");
			query.Set("nameMatchMode", "Auto");
			query.Set("sort", "Name");
			query.Set("discTypes", "Unknown");
			query.Set("artistParticipationStatus", "Everything");
			query.Set("childVoicebanks", "false");
			query.Set("status", "");
			query.Set("deleted", "false");
			string url = string.Format(UrlFormatString, ToQueryString(query));

			// Get response
			HttpResponseMessage response = await GetResponse(url);
			AlbumSearchResponse searchResponse = await JsonSerializer.DeserializeAsync<AlbumSearchResponse>(response.Content.ReadAsStream());

			// Parse response and prepare final list
			foreach (var item in searchResponse.items) {
				RemoteSearchResult artistResult = new RemoteSearchResult {
					Name = item.artistString
				};
				RemoteSearchResult result = new RemoteSearchResult {
					ImageUrl = item.mainPicture?.urlSmallThumb,
					Name = item.name,
					ProductionYear = item.releaseDate.year,
					AlbumArtist = artistResult
				};
				result.SetProviderId(Plugin.ProviderId, item.id.ToString());
				results.Add(result);
			}
			return results;
		}
		public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) {
			return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
		}
	}
}