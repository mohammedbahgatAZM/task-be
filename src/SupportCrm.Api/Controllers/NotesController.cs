namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Customers;

[ApiController]
[Route("api/customers/{customerId:guid}/notes")]
public class NotesController(NoteAndAttachmentService noteAndAttachmentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NoteDto>>> GetAll(Guid customerId, CancellationToken ct) =>
        Ok(await noteAndAttachmentService.GetNotesAsync(customerId, ct));

    [HttpPost]
    public async Task<ActionResult<NoteDto>> Add(Guid customerId, [FromBody] AddNoteRequest request, CancellationToken ct)
    {
        try
        {
            return await noteAndAttachmentService.AddNoteAsync(customerId, request, ct);
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{noteId:guid}/pin")]
    public async Task<IActionResult> SetPinned(Guid noteId, [FromBody] SetNotePinnedRequest request, CancellationToken ct)
    {
        try
        {
            await noteAndAttachmentService.SetNotePinnedAsync(noteId, request, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
