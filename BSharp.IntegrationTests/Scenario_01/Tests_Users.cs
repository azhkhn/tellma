﻿using BSharp.Controllers.Dto;
using BSharp.Entities;
using BSharp.IntegrationTests.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BSharp.IntegrationTests.Scenario_01
{
    public class Tests_Users : Scenario_01
    {
        public const string usersURL = "/api/users";

        public Tests_Users(Scenario_01_WebApplicationFactory factory, ITestOutputHelper output) : base(factory, output)
        {
        }

        [Fact(DisplayName = "01 Getting all Users before creating any returns a 200 OK singleton collection")]
        public async Task Test01()
        {
            var response = await Client.GetAsync(usersURL);

            // Call the API
            Output.WriteLine(await response.Content.ReadAsStringAsync());

            // Assert the result is 200 OK
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Confirm the result is well formed
            var responseData = await response.Content.ReadAsAsync<GetResponse<User>>();

            // Assert the result makes sense
            Assert.Equal("User", responseData.CollectionName);
            Assert.Single(responseData.Result); // First user
        }

        [Fact(DisplayName = "02 Getting a non-existent user id returns a 404 Not Found")]
        public async Task Test02()
        {
            int nonExistentId = 9999999;
            var response = await Client.GetAsync($"{usersURL}/{nonExistentId}");

            Output.WriteLine(await response.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        
        [Fact(DisplayName = "03 Saving a single well-formed user for save returns a 200 OK result")]
        public async Task Test03()
        {
            int chiefOfStaffId = Shared.Get<Role>("Role_ChiefOfStaff").Id;
            int johnWickId = Shared.Get<Agent>("Agent_JohnWick").Id;

            // Prepare a well formed entity
            var dtoForSave = new UserForSave
            {
                Id = johnWickId,
                Email = "ahmad.akra@banan-it.com",
                Roles = new List<RoleMembershipForSave>
                {
                    new RoleMembershipForSave
                    {
                        RoleId = chiefOfStaffId,
                        Memo = "Nice"
                    }
                }
            };

            // Save it
            var dtosForSave = new List<UserForSave> { dtoForSave };
            var response = await Client.PostAsJsonAsync($"{usersURL}?expand=Roles/Role", dtosForSave);

            // Assert that the response status code is a happy 200 OK
            Output.WriteLine(await response.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert that the response is well-formed singleton
            var responseData = await response.Content.ReadAsAsync<EntitiesResponse<User>>();
            Assert.Single(responseData.Result);

            // Assert that the result matches the saved entity
            Assert.Equal("User", responseData.CollectionName);

            var responseDto = responseData.Result.FirstOrDefault();
            Assert.NotNull(responseDto?.Id);
            Assert.Equal(dtoForSave.Email, responseDto.Email);
            Assert.Equal(dtoForSave.Id, responseDto.Id);
            Assert.Collection(responseDto.Roles,
                    p =>
                    {
                        Assert.Equal(dtoForSave.Roles[0].RoleId, p.RoleId);
                        Assert.Equal(dtoForSave.Roles[0].Memo, p.Memo);
                        Assert.NotEqual(0, p.Id);
                    }
                );

            Shared.Set("Users_AhmadAkra", responseDto);
        }
        
        [Fact(DisplayName = "04 Getting the Id of the user just saved returns a 200 OK result")]
        public async Task Test04()
        {
            // Query the API for the Id that was just returned from the Save
            var entity = Shared.Get<User>("Users_AhmadAkra");
            var id = entity.Id;
            var response = await Client.GetAsync($"{usersURL}/{id}?expand=Roles/Role");

            Output.WriteLine(await response.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Confirm that the response is a well formed GetByIdResponse
            var getByIdResponse = await response.Content.ReadAsAsync<GetByIdResponse<User>>();
            Assert.Equal("User", getByIdResponse.CollectionName);

            var responseDto = getByIdResponse.Result;
            Assert.NotNull(responseDto?.Id);
            Assert.Equal(entity.Email, responseDto.Email);
            Assert.Equal(entity.Id, responseDto.Id);
            Assert.Collection(responseDto.Roles,
                    p =>
                    {
                        Assert.Equal(entity.Roles[0].RoleId, p.RoleId);
                        Assert.Equal(entity.Roles[0].Memo, p.Memo);
                        Assert.NotEqual(0, p.Id);
                    }
                );
        }

        [Fact(DisplayName = "05 Saving a user with a non existent role Id returns a 422 Unprocessable Entity")]
        public async Task Test05()
        {
            int jasonId = Shared.Get<Agent>("Agent_JasonBourne").Id;

            // Prepare a unit with the same code as one that has been saved already
            var dtoForSave = new UserForSave
            {
                Id = jasonId,
                Email = "abdullah-ulber", // Wrong email
                Roles = new List<RoleMembershipForSave>
                {
                    new RoleMembershipForSave
                    {
                        RoleId = 9999, // non existent Id
                        Memo = "Nice"
                    }
                }
            };

            // Call the API
            var response = await Client.PostAsJsonAsync(usersURL, new List<UserForSave> { dtoForSave });

            // Assert that the response status code is 422 unprocessable entity (validation errors)
            Output.WriteLine(await response.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

            // Confirm that the result is a well-formed validation errors structure
            var errors = await response.Content.ReadAsAsync<ValidationErrors>();

            // Assert that it contains a validation key pointing to the Email property
            {
                string expectedKey = "[0].Email";
                Assert.True(errors.ContainsKey(expectedKey), $"Expected error key '{expectedKey}' was not found");

                // Assert that it contains a useful error message in English
                var message = errors[expectedKey].Single();
                Assert.Contains("not a valid e-mail address", message.ToLower());
            }

            // Fix the email
            dtoForSave.Email = "jason.bourne@banan-it.com";
            response = await Client.PostAsJsonAsync(usersURL, new List<UserForSave> { dtoForSave });

            // Assert that the response status code is 422 unprocessable entity (validation errors)
            Output.WriteLine(await response.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

            // Confirm that the result is a well-formed validation errors structure
            errors = await response.Content.ReadAsAsync<ValidationErrors>();

            // Assert that it contains a validation key pointing to the RoleId property of the Role line
            {
                string expectedKey = "[0].Roles[0].RoleId";
                Assert.True(errors.ContainsKey(expectedKey), $"Expected error key '{expectedKey}' was not found");

                // Assert that it contains a useful error message in English
                var message = errors[expectedKey].Single();
                Assert.Contains("no longer exists", message.ToLower());
            }
        }
        
        [Fact(DisplayName = "06 Updating a user works as expected")]
        public async Task Test06()
        {
            // Get the entity we just saved
            var id = Shared.Get<User>("Users_AhmadAkra").Id;
            var response1 = await Client.GetAsync($"{usersURL}/{id}?expand=Roles/Role");
            var dto = (await response1.Content.ReadAsAsync<GetByIdResponse<User>>()).Result;

            // Modify it slightly
            dto.Roles[0].Memo = "Nice 2"; // Changed

            // Save it and get the result back
            var dtosForSave = new List<User> { dto };
            var response2 = await Client.PostAsJsonAsync($"{usersURL}?expand=Roles/Role", dtosForSave);
            Output.WriteLine(await response2.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, response2.StatusCode);
            var dto2 = (await response2.Content.ReadAsAsync<EntitiesResponse<User>>()).Result.FirstOrDefault();

            // Confirm it has been changed
            Assert.Equal(dto.Email, dto2.Email);
            Assert.Equal(dto.Id, dto2.Id);
            Assert.Collection(dto2.Roles,
                    p =>
                    {
                        Assert.Equal(dto.Roles[0].RoleId, p.RoleId);
                        Assert.Equal(dto.Roles[0].Memo, p.Memo);
                    }
                );
        }
        
        [Fact(DisplayName = "07 Deleting an existing user Id returns a 200 OK")]
        public async Task Test07()
        {
            // Get the Id
            var id = Shared.Get<User>("Users_AhmadAkra").Id;

            // Query the delete API
            var msg = new HttpRequestMessage(HttpMethod.Delete, usersURL);
            msg.Content = new ObjectContent<List<int>>(new List<int> { id }, new JsonMediaTypeFormatter());
            var deleteResponse = await Client.SendAsync(msg);

            Output.WriteLine(await deleteResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        }

        
        [Fact(DisplayName = "08 Getting an Id that was just deleted returns a 404 Not Found")]
        public async Task Test08()
        {
            // Get the Id
            var id = Shared.Get<User>("Users_AhmadAkra").Id;

            // Verify that the id was deleted by calling get        
            var getResponse = await Client.GetAsync($"{usersURL}/{id}");

            // Assert that the response is correct
            Output.WriteLine(await getResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
    }
}
