using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TampaBay.Models;

namespace TampaBay.Controllers
{
    // All of these routes will be at the base URL:     /api/Events
    // That is what "api/[controller]" means below. It uses the name of the controller
    // in this case EventsController to determine the URL
    [Route("api/[controller]")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        // This is the variable you use to have access to your database
        private readonly DatabaseContext _context;

        // Constructor that recives a reference to your database context
        // and stores it in _context for you to use in your API methods
        public EventsController(DatabaseContext context)
        {
            _context = context;
        }

        // GET: api/Events
        //
        // Returns a list of all your Events
        //
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Event>>> GetEvents(string filter)
        {

            // return await _context.Events.OrderBy(row => row.Id).ToListAsync();

            if (filter == null)
            {
                return await _context.Events.OrderBy(events => events.Name).Include(events => events.Reviews).ToListAsync();
            }
            else
            {
                return await _context.Events.Where(events => events.Name.Contains(filter) ||
                                                                      events.Category.Contains(filter)).Include(events => events.Reviews).ToListAsync();
            }
        }



        // GET: api/Events/5
        //
        // Fetches and returns a specific @event by finding it by id. The id is specified in the
        // URL. In the sample URL above it is the `5`.  The "{id}" in the [HttpGet("{id}")] is what tells dotnet
        // to grab the id from the URL. It is then made available to us as the `id` argument to the method.
        //
        [HttpGet("{id}")]
        public async Task<ActionResult<Event>> GetEvent(int id)
        {
            // Find the restaurant in the database using Include to ensure we have the associated reviews
            var @event = await _context.Events.
                                    Include(@event => @event.Reviews).
                                    ThenInclude(@event => @event.User).
                                    Where(@event => @event.Id == id).FirstOrDefaultAsync();

            // If we didn't find anything, we receive a `null` in return
            if (@event == null)
            {
                // Return a `404` response to the client indicating we could not find a @event with this id
                return NotFound();
            }

            //  Return the @event as a JSON object.
            return @event;
        }

        // PUT: api/Events/5
        //
        // Update an individual @event with the requested id. The id is specified in the URL
        // In the sample URL above it is the `5`. The "{id} in the [HttpPut("{id}")] is what tells dotnet
        // to grab the id from the URL. It is then made available to us as the `id` argument to the method.
        //
        // In addition the `body` of the request is parsed and then made available to us as a Event
        // variable named @event. The controller matches the keys of the JSON object the client
        // supplies to the names of the attributes of our Event POCO class. This represents the
        // new values for the record.
        //
        [HttpPut("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> PutEvent(int id, Event @event)
        {
            // Find this restaurant by looking for the specific id
            var eventBelongsToUser = await _context.Events.AnyAsync(events => events.Id == id && events.UserId == GetCurrentUserId());
            if (!eventBelongsToUser)
            {
                // Make a custom error response
                var response = new
                {
                    status = 401,
                    errors = new List<string>() { "Not Authorized" }
                };

                // Return our error with the custom response
                return Unauthorized(response);
            }
            // If the ID in the URL does not match the ID in the supplied request body, return a bad request
            if (id != @event.Id)
            {
                return BadRequest();
            }

            // Tell the database to consider everything in @event to be _updated_ values. When
            // the save happens the database will _replace_ the values in the database with the ones from @event
            _context.Entry(@event).State = EntityState.Modified;

            try
            {
                // Try to save these changes.
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Ooops, looks like there was an error, so check to see if the record we were
                // updating no longer exists.
                if (!EventExists(id))
                {
                    // If the record we tried to update was already deleted by someone else,
                    // return a `404` not found
                    return NotFound();
                }
                else
                {
                    // Otherwise throw the error back, which will cause the request to fail
                    // and generate an error to the client.
                    throw;
                }
            }

            // return NoContent to indicate the update was done. Alternatively you can use the
            // following to send back a copy of the updated data.
            //
            // return Ok(@event)
            //
            return NoContent();
        }

        // POST: api/Events
        //
        // Creates a new @event in the database.
        //
        // The `body` of the request is parsed and then made available to us as a Event
        // variable named @event. The controller matches the keys of the JSON object the client
        // supplies to the names of the attributes of our Event POCO class. This represents the
        // new values for the record.
        //
        [HttpPost]
        // Dont run code unless you have token
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<ActionResult<Event>> PostEvent(Event @event)
        {
            // Set the UserID to the current user id, this overrides anything the user specifies.
            @event.UserId = GetCurrentUserId();
            // Indicate to the database context we want to add this new record
            _context.Events.Add(@event);
            await _context.SaveChangesAsync();

            // Return a response that indicates the object was created (status code `201`) and some additional
            // headers with details of the newly created object.
            return CreatedAtAction("GetEvent", new { id = @event.Id }, @event);
        }

        // DELETE: api/Events/5
        //
        // Deletes an individual @event with the requested id. The id is specified in the URL
        // In the sample URL above it is the `5`. The "{id} in the [HttpDelete("{id}")] is what tells dotnet
        // to grab the id from the URL. It is then made available to us as the `id` argument to the method.
        //
        [HttpDelete("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            // Find this @event by looking for the specific id
            var @event = await _context.Events.FindAsync(id);
            if (@event == null)
            {
                // There wasn't a @event with that id so return a `404` not found
                return NotFound();
            }
            //tries to use api and isnt the right user.. rejection
            if (@event.UserId != GetCurrentUserId())
            {
                // Make a custom error response
                var response = new
                {
                    status = 401,
                    errors = new List<string>() { "Not Authorized" }
                };

                // Return our error with the custom response
                return Unauthorized(response);
            }

            // Tell the database we want to remove this record
            _context.Events.Remove(@event);

            // Tell the database to perform the deletion
            await _context.SaveChangesAsync();

            // return NoContent to indicate the update was done. Alternatively you can use the
            // following to send back a copy of the deleted data.
            //
            // return Ok(@event)
            //
            return NoContent();
        }

        // Private helper method that looks up an existing @event by the supplied id
        private bool EventExists(int id)
        {
            return _context.Events.Any(@event => @event.Id == id);
        }
        // Private helper method to get the JWT claim related to the user ID
        private int GetCurrentUserId()
        {
            // Get the User Id from the claim and then parse it as an integer.
            return int.Parse(User.Claims.FirstOrDefault(claim => claim.Type == "Id").Value);
        }
    }
}
