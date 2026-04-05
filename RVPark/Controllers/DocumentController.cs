using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class DocumentController : Controller
{
    private readonly UnitOfWork _unitOfWork;
    public DocumentController(UnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    [HttpGet("{id}")]
    [Authorize]
    public IActionResult Download(int id)
    {
        var doc = _unitOfWork.Document.Get(d => d.Id == id);
        if (doc == null || doc.FileData == null) return NotFound();
        // add ownership/admin check here maybe
        return File(doc.FileData, doc.ContentType, doc.FileName);
    }
}