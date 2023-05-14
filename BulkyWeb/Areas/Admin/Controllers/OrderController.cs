using Bulky.DataAccess.Migrations;
using Bulky.DataAccess.Repository.IRepository;
using Bulky.Models;
using Bulky.Models.ViewModels;
using Bulky.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Diagnostics;
using System.Security.Claims;

namespace BulkyWeb.Areas.Admin.Controllers
{
	[Area("admin")]
	[Authorize]
	public class OrderController : Controller
	{
		private readonly IUnitOfWork _unitOfWork;
		[BindProperty]
		public OrderVM OrderVM { get; set; }

		public OrderController(IUnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;  
		}
		public IActionResult Index()
		{
			return View();
		}

		public IActionResult Details(int orderId)
		{
			OrderVM = new()
			{
				OrderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderId, includProperties: "ApplicationUser"),
				OrderDetail = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == orderId, includProperties: "Product")
			};

			return View(OrderVM);
		}

		[HttpPost]
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		public IActionResult UpdateOrderDetail() 
		{
			var orderHeaderFromDB = _unitOfWork.OrderHeader.Get(u=>u.Id==OrderVM.OrderHeader.Id);
			orderHeaderFromDB.Name = OrderVM.OrderHeader.Name;
			orderHeaderFromDB.PhoneNumber = OrderVM.OrderHeader.PhoneNumber;
			orderHeaderFromDB.StreetAddress = OrderVM.OrderHeader.StreetAddress;
			orderHeaderFromDB.City = OrderVM.OrderHeader.City;
			orderHeaderFromDB.State = OrderVM.OrderHeader.State;
			orderHeaderFromDB.PostalCode = OrderVM.OrderHeader.PostalCode;
			if (!string.IsNullOrEmpty(OrderVM.OrderHeader.Carrier))
			{
				orderHeaderFromDB.Carrier = OrderVM.OrderHeader.Carrier;
			}
			if (!string.IsNullOrEmpty(OrderVM.OrderHeader.TrackingNUmber))
			{
				orderHeaderFromDB.TrackingNUmber = OrderVM.OrderHeader.TrackingNUmber;
			}
			_unitOfWork.OrderHeader.Update(orderHeaderFromDB);
			_unitOfWork.Save();

			TempData["Success"] = "Order Details Updated Successfully.";

			return RedirectToAction(nameof(Details), new {orderId = orderHeaderFromDB .Id});
		}

		[HttpPost]
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		public IActionResult StartProcessing()
		{
			_unitOfWork.OrderHeader.UpdateStatus(OrderVM.OrderHeader.Id, SD.StatusInProcess);
			_unitOfWork.Save();

			TempData["Success"] = "Order Details Updated Successfully.";

			return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });

		}

		[HttpPost]
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		public IActionResult ShipOrder()
		{

			var orderHeaderFromDB = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);
			orderHeaderFromDB.TrackingNUmber = OrderVM.OrderHeader.TrackingNUmber;
			orderHeaderFromDB.Carrier = OrderVM.OrderHeader.Carrier;
			orderHeaderFromDB.Orderstatus = OrderVM.OrderHeader.Orderstatus;
			orderHeaderFromDB.ShippingDate = DateTime.Now;
			if(orderHeaderFromDB.PaymentStatus == SD.PaymentStatusDelayedPayment)
			{
				orderHeaderFromDB.PaymentDueDate = DateOnly.FromDateTime(DateTime.Now.AddDays(30));
			}
			_unitOfWork.OrderHeader.Update(orderHeaderFromDB);
			_unitOfWork.Save();


			_unitOfWork.OrderHeader.UpdateStatus(OrderVM.OrderHeader.Id, SD.StatusShipped);
			_unitOfWork.Save();

			TempData["Success"] = "Order Shipped Successfully.";

			return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });

		}


		[HttpPost]
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		public IActionResult CancelOrder()
		{

			var orderHeaderFromDB = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id);
			if (orderHeaderFromDB.PaymentStatus == SD.PaymentStatusApproved)
			{
				var options = new RefundCreateOptions
				{
					Reason = RefundReasons.RequestedByCustomer,
					PaymentIntent = orderHeaderFromDB.PaymentIntentId
				};
				var service = new RefundService();
				Refund refund = service.Create(options);
				_unitOfWork.OrderHeader.UpdateStatus(orderHeaderFromDB.Id, SD.StatusCancelled, SD.StatusRefunded);
			}
			else
			{
				_unitOfWork.OrderHeader.UpdateStatus(orderHeaderFromDB.Id, SD.StatusCancelled, SD.StatusCancelled);
			}
			_unitOfWork.Save();

			TempData["Success"] = "Order Cancelled Successfully.";

			return RedirectToAction(nameof(Details), new { orderId = OrderVM.OrderHeader.Id });

		}

		[HttpPost]
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		[ActionName("Details")]
		public IActionResult Details_PAY_NOW()
		{
			OrderVM.OrderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == OrderVM.OrderHeader.Id, includProperties: "ApplicationUser");
			OrderVM.OrderDetail = _unitOfWork.OrderDetail.GetAll(u => u.OrderHeaderId == OrderVM.OrderHeader.Id, includProperties: "Product");

			//stripe logic

			var domain = "https://localhost:7223/";
			var options = new SessionCreateOptions
			{
				SuccessUrl = domain + $"admin/order/PaymentConfirmation?orderHeaderId={OrderVM.OrderHeader.Id}",
				CancelUrl = domain + $"admin/order/details?orderId={OrderVM.OrderHeader.Id}",
				LineItems = new List<SessionLineItemOptions>(),
				Mode = "payment",
			};

			foreach (var item in OrderVM.OrderDetail)
			{
				var sessionLineItem = new SessionLineItemOptions
				{
					PriceData = new SessionLineItemPriceDataOptions
					{
						UnitAmount = (long)(item.Price * 100), //20.50 => 2050
						Currency = "GBP",//RegionInfo.CurrentRegion.ISOCurrencySymbol;
						ProductData = new SessionLineItemPriceDataProductDataOptions
						{
							Name = item.Product.Title

						}
					},
					Quantity = item.Count
				};
				options.LineItems.Add(sessionLineItem);
			}
			var service = new SessionService();
			Session session = service.Create(options);

			_unitOfWork.OrderHeader.UpdatetStripePaymentId(OrderVM.OrderHeader.Id, session.Id, session.PaymentIntentId);
			_unitOfWork.Save();

			Response.Headers.Add("Location", session.Url);
			return new StatusCodeResult(303);

		}

		public IActionResult PaymentConfirmation(int orderHeaderId)
		{
			OrderHeader orderHeader = _unitOfWork.OrderHeader.Get(u => u.Id == orderHeaderId, includProperties: "ApplicationUser");
			if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
			{
				//company order
				var service = new SessionService();
				Session session = service.Get(orderHeader.SessionId);

				if (session.PaymentStatus.ToLower() == "paid")
				{
					_unitOfWork.OrderHeader.UpdatetStripePaymentId(orderHeaderId, session.Id, session.PaymentIntentId);
					_unitOfWork.OrderHeader.UpdateStatus(orderHeaderId, orderHeader.Orderstatus, SD.PaymentStatusApproved);
					_unitOfWork.Save();
				}
			}

			return View(orderHeaderId);
		}

		#region API CALLS

		[HttpGet]
		public IActionResult GetAll(string status)
		{
			IEnumerable<OrderHeader> objOrderHeaders;

			if(User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
			{
				objOrderHeaders = _unitOfWork.OrderHeader.GetAll(includProperties: "ApplicationUser").ToList();
			}
			else
			{
				var claimsIdentity = (ClaimsIdentity)User.Identity;
				var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;

				objOrderHeaders = _unitOfWork.OrderHeader.GetAll(u=> u.ApplicationUserId == userId, includProperties: "ApplicationUser").ToList();
			}
			switch (status)
			{
				case "pending":
					objOrderHeaders = objOrderHeaders.Where(u => u.PaymentStatus == SD.PaymentStatusDelayedPayment);
					break;
				case "inprocess":
					objOrderHeaders = objOrderHeaders.Where(u => u.Orderstatus == SD.StatusInProcess);
					break;
				case "completed":
					objOrderHeaders = objOrderHeaders.Where(u => u.Orderstatus == SD.StatusShipped);
					break;
				case "approved":
					objOrderHeaders = objOrderHeaders.Where(u => u.Orderstatus == SD.StatusApproved);
					break;
				default:
					break;
			}
			return Json(new { data = objOrderHeaders });
		}


		#endregion

	}
}
