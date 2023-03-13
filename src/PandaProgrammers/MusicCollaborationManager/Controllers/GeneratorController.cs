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
using Microsoft.AspNetCore.Mvc.Rendering;
using MusicCollaborationManager.Services.Abstract;
using MusicCollaborationManager.Utilities;
using Humanizer.Localisation;

namespace MusicCollaborationManager.Controllers
{
    public class GeneratorController : Controller
    {
        private readonly IListenerRepository _listenerRepository;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SpotifyAuthService _spotifyService;
        private readonly IDeepAiService _deepAiService;

        public GeneratorController(IListenerRepository listenerRepository, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, SpotifyAuthService spotifyService, IDeepAiService deepAiService)
        {
            _listenerRepository = listenerRepository;
            _userManager = userManager;
            _signInManager = signInManager;
            _spotifyService = spotifyService;
            _deepAiService = deepAiService;
        }

        [Authorize]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult Questionaire(QuestionViewModel vm)
        {
            try
            {
                var holder = _spotifyService.GetSeedGenres();
                var seededVM = vm.SeedGenres(vm, holder);

                return View("Questionaire", seededVM);
            }
            catch (Exception e)
            {
                return RedirectToAction("callforward", "Home");
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> QuestionairePostAsync(QuestionViewModel vm)
        {
            try
            {
                GeneratorsViewModel generatorsViewModel = new GeneratorsViewModel();
                string UserInputCoverImage = vm.coverImageInput;
                GeneratorUtilities utilities = new GeneratorUtilities();

                RecommendDTO recommendDTO = new RecommendDTO();
                //Calls questionairre dto method
                recommendDTO = recommendDTO.convertToQuestionDTO(vm);
                //Get seed artist
                List<string> artistResult = await _spotifyService.searchTopGenrePlaylistArtist(recommendDTO.genre[0]);
                recommendDTO.seed.Add(artistResult[0]);

                RecommendationsResponse response = await _spotifyService.GetRecommendations(recommendDTO);
                List<SimpleTrack> result = new List<SimpleTrack>();
                result = response.Tracks;

                generatorsViewModel.fullResult = await _spotifyService.ConvertToFullTrack(result);

                generatorsViewModel.PlaylistCoverImageUrl = _deepAiService.GetImageUrlFromApi(UserInputCoverImage);

                return View("GeneratedPlaylists", generatorsViewModel);
            }
            catch (Exception e) 
            {
                //Error occurs when not logged into spotify
                return RedirectToAction("callforward", "Home");
            }

        }

        [Authorize]
        public IActionResult Mood(MoodViewModel vm)
        {
            try
            {
                return View("Mood", vm);
            }
            catch (Exception e)
            {
                return RedirectToAction("callforward", "Home");
            }

        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> MoodPostAsync(MoodViewModel vm)
        {
            try
            {
                GeneratorsViewModel generatorsViewModel = new GeneratorsViewModel();
                string UserInputCoverImage = vm.coverImageInput;

                RecommendDTO recommendDTO = new RecommendDTO();
                //Calls mood dto method
                recommendDTO = recommendDTO.convertToMoodDTO(vm);                
                
                List<string> trackResult = await _spotifyService.searchTopGenrePlaylistArtist(recommendDTO.genre[0]);
                foreach (string track in trackResult)
                {
                    recommendDTO.seed.Add(track);
                }
                RecommendationsResponse response = await _spotifyService.GetRecommendations(recommendDTO);
                List<SimpleTrack> result = new List<SimpleTrack>();
                result = response.Tracks;

                generatorsViewModel.fullResult = await _spotifyService.ConvertToFullTrack(result);

                generatorsViewModel.PlaylistCoverImageUrl = _deepAiService.GetImageUrlFromApi(UserInputCoverImage);

                return View("GeneratedPlaylists", generatorsViewModel);

            }
            catch (Exception e)
            {
                //Error occurs when not logged into spotify
                return RedirectToAction("callforward", "Home");
            }
        }

        [Authorize]
        public IActionResult Time(TimeViewModel vm)
        {
            try
            {
                return View("Time", vm);
            }
            catch (Exception e)
            {
                return RedirectToAction("callforward", "Home");
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> TimePostAsync(TimeViewModel vm)
        {
            try
            {
                GeneratorsViewModel generatorsViewModel = new GeneratorsViewModel();
                string UserInputCoverImage = vm.coverImageInput;
                RecommendDTO recommendDTO = new RecommendDTO();
                GeneratorUtilities generatorUtilities = new GeneratorUtilities();

                //Sets time category
                vm.timeCategory = generatorUtilities.getTimeValue(DateTime.Now);
                //Calls time dto method                
                recommendDTO = recommendDTO.convertToTimeDTO(vm);

                List<string> trackResult = await _spotifyService.searchTopGenrePlaylistArtist(recommendDTO.genre[0]);
                foreach (string track in trackResult)
                {
                    recommendDTO.seed.Add(track);
                }

                RecommendationsResponse response = await _spotifyService.GetRecommendations(recommendDTO);
                List<SimpleTrack> result = new List<SimpleTrack>();
                result = response.Tracks;

                generatorsViewModel.fullResult = await _spotifyService.ConvertToFullTrack(result);

                generatorsViewModel.PlaylistCoverImageUrl = _deepAiService.GetImageUrlFromApi(UserInputCoverImage);

                return View("GeneratedPlaylists", generatorsViewModel);

            }
            catch (Exception e)
            {
                //Error occurs when not logged into spotify
                return RedirectToAction("callforward", "Home");
            }
        }

        [Authorize]
        public IActionResult GeneratedPlaylists()
        {
            return View();
        }
    }
}