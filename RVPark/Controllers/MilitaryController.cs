using ApplicationCore.Interfaces;
using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace RVPark.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MilitaryController : Controller
    {
        private readonly UnitOfWork _unitOfWork;

        public MilitaryController(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }


        //Gets all branches for data table
        [HttpGet("GetMilitaryBranches")]
        public IActionResult GetMilitaryBranches()
        {
            var branches = _unitOfWork.MilitaryBranch.GetAll();

            // Format data for the DataTable, now including IsActive
            var data = branches.Select(b => new
            {
                b.Id,
                BranchName = b.BranchName,
                IsActive = b.IsActive
            });

            return Json(new { data });
        }

        //Gets all ranks for data table
        [HttpGet("GetMilitaryRanks")]
        public IActionResult GetMilitaryRanks()
        {
            var ranks = _unitOfWork.MilitaryRank.GetAll(
                predicate: null
            );

            // Format data for the DataTable, now including IsActive
            var data = ranks.Select(r => new
            {
                r.Id,
                Rank = r.Rank,
                IsActive = r.IsActive
            });

            return Json(new { data });
        }

        //Gets all statuses for data table
        [HttpGet("GetMilitaryStatuses")]
        public IActionResult GetMilitaryStatuses()
        {
            var statuses = _unitOfWork.MilitaryStatus.GetAll();

            // Format data for the DataTable
            var data = statuses.Select(s => new
            {
                s.Id,
                Status = s.Status
            });

            return Json(new { data });
        }

        [HttpGet("GetRanksByBranch")]
        public IActionResult GetRanksByBranch(int branchId)
        {
            try
            {
                if (branchId <= 0)
                {
                    return BadRequest("Invalid branch ID");
                }

                var ranks = _unitOfWork.MilitaryRank
                    .GetAll(r => r.IsActive) // Filter for active ranks only
                    .Select(r => new { id = r.Id, rank = r.Rank })
                    .ToList();

                return Json(ranks);
            }
            catch (Exception ex)
            {
                // Log the exception if you have logging configured
                return StatusCode(500, "An error occurred while retrieving ranks");
            }
        }

        [HttpDelete("DeleteBranch")]
        public IActionResult DeleteBranch(int branchId)
        {
            Console.WriteLine("==============DELETING==================");
            var objFromDb = _unitOfWork.MilitaryBranch.Get(b => b.Id == branchId);
            if (objFromDb == null)
            {
                return Json(new { success = false, message = "Error while deleting" });
            }

            _unitOfWork.MilitaryBranch.Delete(objFromDb);
            _unitOfWork.Commit();

            return Json(new { success = true, message = "Delete Succesful" });
        }

        [HttpDelete("DeleteRank")]
        public IActionResult DeleteRank(int rankId)
        {
            Console.WriteLine("==============DELETING RANK==================");
            var objFromDb = _unitOfWork.MilitaryRank.Get(r => r.Id == rankId);
            if (objFromDb == null)
            {
                return Json(new { success = false, message = "Error while deleting" });
            }

            _unitOfWork.MilitaryRank.Delete(objFromDb);
            _unitOfWork.Commit();

            return Json(new { success = true, message = "Delete Succesful" });
        }

        [HttpDelete("DeleteStatus")]
        public IActionResult DeleteStatus(int statusId)
        {
            Console.WriteLine("==============DELETING==================");
            var objFromDb = _unitOfWork.MilitaryStatus.Get(s => s.Id == statusId);
            if (objFromDb == null)
            {
                return Json(new { success = false, message = "Error while deleting" });
            }

            _unitOfWork.MilitaryStatus.Delete(objFromDb);
            _unitOfWork.Commit();

            return Json(new { success = true, message = "Delete Succesful" });
        }
    }
}
