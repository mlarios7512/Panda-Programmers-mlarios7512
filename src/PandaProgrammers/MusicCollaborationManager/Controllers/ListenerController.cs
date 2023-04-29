﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MusicCollaborationManager.DAL.Abstract;
using MusicCollaborationManager.Models;
using MusicCollaborationManager.ViewModels;
using MusicCollaborationManager.Services.Concrete;
using MusicCollaborationManager.Models.DTO;
using SpotifyAPI.Web;
using static NuGet.Packaging.PackagingConstants;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.RegularExpressions;
using MusicCollaborationManager.Services.Abstract;
using System.Diagnostics;

namespace MusicCollaborationManager.Controllers
{
    public class ListenerController : Controller
    {
        private readonly IListenerRepository _listenerRepository;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SpotifyAuthService _spotifyService;
        private readonly IPollsService _pollsService;
        private readonly IPlaylistPollRepository _playlistPollRepository;


        public ListenerController(
            IListenerRepository listenerRepository,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            SpotifyAuthService spotifyService,
            IPollsService pollsService,
            IPlaylistPollRepository playlistPollRepository
        )
        {
            _listenerRepository = listenerRepository;
            _userManager = userManager;
            _signInManager = signInManager;
            _spotifyService = spotifyService;
            _pollsService = pollsService;
            _playlistPollRepository = playlistPollRepository;
        }

        [Authorize]
        public async Task<IActionResult> Index(UserDashboardViewModel vm)
        {
            string aspId = _userManager.GetUserId(User);

            Listener listener = new Listener();

            listener = _listenerRepository.FindListenerByAspId(aspId);

            if (listener.SpotifyId != null)
            {
                await _spotifyService.GetCallbackAsync("", listener);
                _listenerRepository.AddOrUpdate(listener);
            }

            vm.fullName = _listenerRepository.GetListenerFullName(listener.Id);

            vm.listener = listener;

            vm.aspUser = User;

            try
            {
                vm.TopTracks = await _spotifyService.GetAuthTopTracksAsync();
                vm.FeatPlaylists = await _spotifyService.GetAuthFeatPlaylistsAsync();
                vm.UserPlaylists = await _spotifyService.GetAuthPersonalPlaylistsAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return RedirectToAction("callforward", "Home");
            }

            return View(vm);
        }

        [Authorize]
        public IActionResult Profile(UserProfileViewModel vm)
        {
            string aspId = _userManager.GetUserId(User);
            Listener listener = new Listener();
            listener = _listenerRepository.FindListenerByAspId(aspId);
            vm.fullName = _listenerRepository.GetListenerFullName(listener.Id);
            vm.listener = listener;
            vm.aspUser = User;

            try
            {
                var holder = _spotifyService.GetAuthUserAsync();

                vm.spotifyName = holder.Result.DisplayName;
                vm.accountType = holder.Result.Product;
                vm.country = holder.Result.Country;
                vm.followerCount = holder.Result.Followers.Total;
                if (holder.Result.Images.Count > 0)
                {
                    vm.profilePic = holder.Result.Images[0].Url;
                }
                else
                {
                    vm.profilePic =
                        "https://t4america.org/wp-content/uploads/2016/10/Blank-User.jpg";
                }
            }
            catch (Exception)
            {
                vm.spotifyName = "Log in to see";
                vm.accountType = "Log in to see";
                vm.country = "Log in to see";
                vm.followerCount = 0;
                vm.profilePic = "https://t4america.org/wp-content/uploads/2016/10/Blank-User.jpg";
            }

            return View(vm);
        }

        [Authorize]
        public IActionResult Settings(Listener listener)
        {
            return View(_listenerRepository.FindListenerByAspId(_userManager.GetUserId(User)));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult<Task> EditListenerInformation(
            [Bind("FirstName,LastName")] Listener listener
        )
        {
            if (
                Regex.IsMatch(listener.FirstName, @"^[a-zA-Z]+$") == false
                || Regex.IsMatch(listener.LastName, @"^[a-zA-Z]+$") == false
            )
            {
                ViewBag.Message = "Name must not contain numbers or special characters";
                return View("Settings");
            }

            ModelState.ClearValidationState("FriendId");
            ModelState.ClearValidationState("AspnetIdentityId");
            ModelState.ClearValidationState("SpotifyId");

            listener.FriendId = 0;
            listener.AspnetIdentityId = _userManager.GetUserId(User);
            listener.SpotifyId = null;

            TryValidateModel(listener);

            if (ModelState.IsValid)
            {
                try
                {
                    Listener oldListener = _listenerRepository.FindListenerByAspId(
                        _userManager.GetUserId(User)
                    );

                    if (oldListener.AspnetIdentityId.Equals(listener.AspnetIdentityId))
                    {
                        _listenerRepository.Delete(oldListener);
                        _listenerRepository.AddOrUpdate(listener);
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    ViewBag.Message =
                        "A concurrency error occurred while trying to create the item.  Please try again.";
                    return View("Settings");
                }
                catch (DbUpdateException)
                {
                    ViewBag.Message =
                        "An unknown database error occurred while trying to create the item.  Please try again.";
                    return View("Settings");
                }

                return RedirectToAction(nameof(Profile));
            }
            else
            {
                ViewBag.Message = "Model state is invalid";
                return View("Settings");
            }
        }

        [Authorize]
        public async Task<IActionResult> Playlist(string playlistID) {

            /*Needs ViewModel with:
                - username (of current MCM user) | Maybe the ID will work instead?
                - Number of follower for this playlist (on spotify)
             */
            
            

            try {

                string aspId = _userManager.GetUserId(User);
                Listener listener;
                listener = _listenerRepository.FindListenerByAspId(aspId);

                FullPlaylistDTO returnPlaylist = new FullPlaylistDTO();
                List<UserTrackDTO> tracks = new List<UserTrackDTO>();
                FullPlaylist convertPlaylist = await _spotifyService.GetPlaylistFromIDAsync(playlistID);
                
                returnPlaylist.LinkToPlaylist = convertPlaylist.Href;
                returnPlaylist.Name = convertPlaylist.Name;
                returnPlaylist.ImageURL = convertPlaylist.Images[0].Url;
                returnPlaylist.Uri = convertPlaylist.Uri;
                returnPlaylist.Owner = convertPlaylist.Owner.DisplayName;
                returnPlaylist.Desc = convertPlaylist.Description;
                returnPlaylist.PlaylistId = playlistID;
                returnPlaylist.ListenerId = listener.Id;


                foreach (PlaylistTrack<IPlayableItem> item in convertPlaylist.Tracks.Items){
                    UserTrackDTO currentTrack = new UserTrackDTO();
                    if (item.Track is FullTrack track) {
                        currentTrack.LinkToTrack = track.Href;
                        currentTrack.Title = track.Name;
                        currentTrack.Artist = track.Artists[0].Name;
                        currentTrack.ImageURL = track.Album.Images[0].Url;
                        currentTrack.Uri = track.Uri;
                        tracks.Add(currentTrack);
                    }
                    if (item.Track is FullEpisode episode) {
                        continue;
                    }
                }

                //Polls stuff (below)------------
                PlaylistViewModel PlaylistView = new PlaylistViewModel();
                PlaylistView.NumPlaylistFollowers = convertPlaylist.Followers.Total;
                //Console.WriteLine("Num playlist followers: " + PlaylistView.NumPlaylistFollowers);
                //Debug.WriteLine("Num playlist followers: " + PlaylistView.NumPlaylistFollowers);

                //If "_playlistPollService" does not have the current spotify playlist id in it, ignore the lines below.
                Poll? PlaylistPollInfo = _playlistPollRepository.GetPollDetailsBySpotifyPlaylistID(returnPlaylist.PlaylistId);

                string userEmail = _userManager.Users.Single(x => x.Id == aspId).Email;
                PlaylistView.MCMUsername = userEmail;
                //Not 'null' indicates a poll is in progress.
                if (PlaylistPollInfo != null)
                {
                    PlaylistView.TrackBeingPolled = new VotingTrack();
                    FullTrack TrackDetails = await _spotifyService.GetSpotifyTrackByID(PlaylistPollInfo.SpotifyTrackUri, SpotifyAuthService.GetTracksClientAsync());
                    PlaylistView.TrackBeingPolled.Artist = TrackDetails.Artists[0].Name;
                    PlaylistView.TrackBeingPolled.Name = TrackDetails.Name;
                    PlaylistView.TrackBeingPolled.Duration = TrackDetails.DurationMs.ToString();

                    //Just needed this to know the option_id of "yes" & "no" for that specific poll, in order to the user what they voted for.
                    IEnumerable<OptionInfoDTO> PollOptions = await _pollsService.GetPollOptionsByPollID(PlaylistPollInfo.PollId);
                    if(PollOptions != null)
                    {
                        PlaylistView.PlaylistVoteOptions = PollOptions;
                    }
                    
                   
                    VoteIdentifierInfoDTO CurUserVote = await _pollsService.GetSpecificUserVoteForAGivenPlaylist(PlaylistPollInfo.PollId, userEmail);
                    

                    foreach (OptionInfoDTO voteOption in PollOptions)
                    {
                        PlaylistView.TrackBeingPolled.TotalVotes += voteOption.OptionCount;
                    }

                    if (CurUserVote != null)
                    {
                        foreach(OptionInfoDTO voteOption in PollOptions) 
                        {
                          if(CurUserVote.OptionID == voteOption.OptionID) 
                            {
                                PlaylistView.TrackBeingPolled.CurUserVoteOption = voteOption.OptionText;
                                break;
                            }
                        }
                    }

                }

                //Polls stuff only (above)---------


                returnPlaylist.Tracks = tracks;
                PlaylistView.PlaylistContents.Tracks = new List<UserTrackDTO>();
                PlaylistView.PlaylistContents = returnPlaylist;
                return View("Playlist", PlaylistView);

                //This needs to incorporate the ViewModel (to do later).
            } catch(ArgumentException e) {
                Console.WriteLine(e.Message);

                FullPlaylistDTO returnPlaylist = new FullPlaylistDTO();
                List<UserTrackDTO> tracks = new List<UserTrackDTO>();
                FullPlaylist convertPlaylist = await _spotifyService.GetPlaylistFromIDAsync("0wbYwQItyK648wmeNcqP5z");
                
                returnPlaylist.LinkToPlaylist = convertPlaylist.Href;
                returnPlaylist.Name = convertPlaylist.Name;
                returnPlaylist.ImageURL = convertPlaylist.Images[0].Url;
                returnPlaylist.Uri = convertPlaylist.Uri;
                returnPlaylist.Owner = convertPlaylist.Owner.DisplayName;
                returnPlaylist.Desc = convertPlaylist.Description;
                returnPlaylist.PlaylistId = "0wbYwQItyK648wmeNcqP5z";

                foreach (PlaylistTrack<IPlayableItem> item in convertPlaylist.Tracks.Items){
                    UserTrackDTO currentTrack = new UserTrackDTO();
                    if (item.Track is FullTrack track) {
                        currentTrack.LinkToTrack = track.Href;
                        currentTrack.Title = track.Name;
                        currentTrack.Artist = track.Artists[0].Name;
                        currentTrack.ImageURL = track.Album.Images[0].Url;
                        currentTrack.Uri = track.Uri;
                        tracks.Add(currentTrack);
                    }
                    if (item.Track is FullEpisode episode) {
                        continue;
                    }
                }
                
                returnPlaylist.Tracks = tracks;
                return View("Playlist", returnPlaylist);
            }
        }
    }
}
