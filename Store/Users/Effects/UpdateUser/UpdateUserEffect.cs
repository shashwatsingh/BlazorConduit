﻿using BlazorConduit.Models.Authentication.ViewModels;
using BlazorConduit.Models.Common;
using BlazorConduit.Services;
using BlazorConduit.Store.Users.Actions.UpdateUser;
using Fluxor;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace BlazorConduit.Store.Users.Effects.UpdateUser
{
    public class UpdateUserEffect : Effect<UpdateUserAction>
    {
        private readonly ConduitApiService _apiService;
        private readonly ErrorFormattingService _formattingService;
        private readonly ILogger<UpdateUserEffect> _logger;
        private readonly SecurityTokenService _tokenService;

        public UpdateUserEffect(
            ConduitApiService apiService,
            ErrorFormattingService formattingService,
            ILogger<UpdateUserEffect> logger,
            SecurityTokenService tokenService)
        {
            _apiService = apiService;
            _formattingService = formattingService;
            _logger = logger;
            _tokenService = tokenService;
        }

        protected override async Task HandleAsync(UpdateUserAction action, IDispatcher dispatcher)
        {
            try
            {
                // Call the register user endpoint with the constructed payload
                var updateResponse = await _apiService.PutAsync("user", action.RequestModel, await _tokenService.GetTokenAsync());

                if (!updateResponse.IsSuccessStatusCode)
                {
                    // Grab the error response from the API and convert them to singularly enumerable strings
                    var rawErrorResponse = await updateResponse.Content.ReadFromJsonAsync<object>();
                    var friendlyErrorResponses = _formattingService.GetFriendlyErrors(rawErrorResponse);

                    // Throw the exception to issue the failure action
                    throw new ConduitApiException(
                        $"Could not update user {action.RequestModel.User.Email}, status: {updateResponse.StatusCode}",
                        updateResponse.StatusCode,
                        friendlyErrorResponses);
                }

                // Registration was successful, issue the success action to set the current user state
                var user = await updateResponse.Content.ReadFromJsonAsync<ConduitUserViewModel>();

                if (user is null || user.User is null)
                {
                    throw new ConduitApiException("No user returned from successful API response", HttpStatusCode.InternalServerError);
                }

                dispatcher.Dispatch(new UpdateUserSuccessAction(user.User));
            }
            catch (ConduitApiException e)
            {
                _logger.LogError($"Validation error during profile update for user with email {action.RequestModel.User.Email}");
                dispatcher.Dispatch(new UpdateUserFailureAction(e.Message, e.ApiErrors));
            }
            catch (Exception e)
            {
                _logger.LogError($"message: {e.Message}\nstacktrace: {e.StackTrace}");
                _logger.LogError($"Error updating user with email {action.RequestModel.User.Email}");
                dispatcher.Dispatch(new UpdateUserFailureAction(e.Message));
            }
        }
    }
}
