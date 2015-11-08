﻿using Microsoft.AspNet.Mvc;
using Microsoft.AspNet.Authorization;
using Microsoft.AspNet.Identity;

using AllReady.Extensions;
using AllReady.Models;

using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AllReady.Services;
using AllReady.Areas.Admin.Models;
using AllReady.Security;
using Microsoft.Framework.Logging;
using System;

namespace AllReady.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize("SiteAdmin")]
    public class SiteController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IAllReadyDataAccess _dataAccess;
        private ILogger<SiteController> _logger;

        public SiteController(UserManager<ApplicationUser> userManager, IEmailSender emailSender, IAllReadyDataAccess dataAccess, ILogger<SiteController> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _dataAccess = dataAccess;
            _logger = logger;
        }

        public IActionResult Index()
        {
            var viewModel = new SiteAdminModel()
            {
                Users = _dataAccess.Users.OrderBy(u => u.UserName).ToList()
            };
            return View(viewModel);
        }

        public IActionResult EditUser(string userId)
        {
            var user = _dataAccess.GetUser(userId);
            var tenantId = user.GetTenantId();
            var viewModel = new EditUserModel()
            {
                UserId = userId,
                UserName = user.UserName,
                AssociatedSkills = user.AssociatedSkills,
                IsTenantAdmin = user.IsUserType(UserType.TenantAdmin),
                IsSiteAdmin = user.IsUserType(UserType.SiteAdmin),
                Tenant = tenantId != null ? _dataAccess.GetTenant(tenantId.Value) : null
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            //Skill associations
            var user = _dataAccess.GetUser(viewModel.UserId);
            user.AssociatedSkills.RemoveAll(usk => viewModel.AssociatedSkills == null || !viewModel.AssociatedSkills.Any(msk => msk.SkillId == usk.SkillId));
            if (viewModel.AssociatedSkills != null)
            {
                user.AssociatedSkills.AddRange(viewModel.AssociatedSkills.Where(msk => !user.AssociatedSkills.Any(usk => usk.SkillId == msk.SkillId)));
            }
            if (user.AssociatedSkills != null && user.AssociatedSkills.Count > 0)
            {
                user.AssociatedSkills.ForEach(usk => usk.UserId = user.Id);
            }
            await _dataAccess.UpdateUser(user);

            var tenantAdminClaim = new Claim(Security.ClaimTypes.UserType, "TenantAdmin");
            if (viewModel.IsTenantAdmin)
            {
                //add tenant admin claim
                var result = await _userManager.AddClaimAsync(user, tenantAdminClaim);
                if (result.Succeeded)
                {
                    var callbackUrl = Url.Action("Login", "Admin", new { Email = user.Email }, protocol: HttpContext.Request.Scheme);
                    await _emailSender.SendEmailAsync(user.Email, "Account Approval", "Your account has been approved by an administrator. Please <a href=" + callbackUrl + ">Click here to Log in</a>");
                }
                else
                {
                    return Redirect("Error");
                }
            }
            else if (user.IsUserType(UserType.TenantAdmin))
            {
                //remove tenant admin claim
                var result = await _userManager.RemoveClaimAsync(user, tenantAdminClaim);
                if (!result.Succeeded)
                {
                    return Redirect("Error");
                }
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string userId)
        {
            try
            {
                var user = _dataAccess.GetUser(userId);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    ViewBag.ErrorMessage = $"Failed to reset password for {user.UserName}, email not confirmed.";
                    return View();
                }

                // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=532713
                // Send an email with this link
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.Action("ResetPassword", "Admin", new { userId = user.Id, code = code }, protocol: HttpContext.Request.Scheme);
                //await _emailSender.SendEmailAsync(user.Email, "Reset Password",
                //   "Please reset your password by clicking here: <a href=\"" + callbackUrl + "\">link</a>");
                ViewBag.SuccessMessage = $"Sent password reset email for {user.UserName}.";
                return View();

            }
            catch (Exception ex)
            {
                _logger.LogError(@"Failed to reset password for {userId}", ex);
                ViewBag.ErrorMessage = $"Failed to reset password for {userId}. Exception thrown.";
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> AssignSiteAdmin(string userId)
        {
            try
            {
                var user = _dataAccess.GetUser(userId);
                await _userManager.AddClaimAsync(user, new Claim(Security.ClaimTypes.UserType, UserType.SiteAdmin.ToName()));
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(@"Failed to assign site admin for {userId}", ex);
                ViewBag.ErrorMessage = $"Failed to assign site admin for {userId}. Exception thrown.";
                return View();
            }
        }

        [HttpGet]
        public IActionResult AssignTenantAdmin(string userId)
        {
            try
            {
                var user = _dataAccess.GetUser(userId);

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(@"Failed to assign site admin for {userId}", ex);
                ViewBag.ErrorMessage = $"Failed to assign site admin for {userId}. Exception thrown.";
                return View();
            }
        }

        [HttpGet]
        public async Task<IActionResult> RevokeSiteAdmin(string userId)
        {
            try
            {
                var user = _dataAccess.GetUser(userId);
                await _userManager.RemoveClaimAsync(user, new Claim(Security.ClaimTypes.UserType, UserType.SiteAdmin.ToName()));
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(@"Failed to assign site admin for {userId}", ex);
                ViewBag.ErrorMessage = $"Failed to assign site admin for {userId}. Exception thrown.";
                return View();
            }
        }

        [HttpGet]
        public IActionResult RevokeTenantAdmin(string userId)
        {
            try
            {
                var user = _dataAccess.GetUser(userId);

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(@"Failed to assign site admin for {userId}", ex);
                ViewBag.ErrorMessage = $"Failed to assign site admin for {userId}. Exception thrown.";
                return View();
            }
        }
    }
}