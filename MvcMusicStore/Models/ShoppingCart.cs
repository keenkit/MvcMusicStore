using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MvcMusicStore.Models
{
    public partial class ShoppingCart
    {
        readonly MusicStoreEntities _storeDb = new MusicStoreEntities();
        string ShoppingCartId { get; set; }
        public const string CartSessionKey = "CartId";

        public static ShoppingCart GetCart(HttpContextBase context)
        {
            var cart = new ShoppingCart();
            cart.ShoppingCartId = cart.GetCartId(context);
            return cart;
        }

        // Helper method to simplify shopping cart calls
        public static ShoppingCart GetCart(Controller controller)
        {
            return GetCart(controller.HttpContext);
        }

        public void AddToCart(Album album)
        {
            // Get the matching cart and album instances
            var cartItem = _storeDb.Carts.SingleOrDefault(
                c => c.CartId == ShoppingCartId
                     && c.AlbumId == album.AlbumId);
            if (cartItem == null)
            {
                // Create a new cart item if no cart item exists
                cartItem = new Cart
                {
                    AlbumId = album.AlbumId,
                    CartId = ShoppingCartId,
                    Count = 1,
                    DateCreated = DateTime.Now
                };
                _storeDb.Carts.Add(cartItem);
            }
            else
            {
                // If the item does exist in the cart, then add one to the quantity
                cartItem.Count++;
            }
            // Save changes
            _storeDb.SaveChanges();
        }

        public int RemoveFromCart(int id)
        {
            // Get the cart
            var cartItem = _storeDb.Carts.Single(
                cart => cart.CartId == ShoppingCartId
                        && cart.RecordId == id);
            var itemCount = 0;
            if (cartItem != null)
            {
                if (cartItem.Count > 1)
                {
                    cartItem.Count--;
                    itemCount = cartItem.Count;
                }
                else
                {
                    _storeDb.Carts.Remove(cartItem);
                }
                // Save changes
                _storeDb.SaveChanges();
            }
            return itemCount;
        }

        public void EmptyCart()
        {
            var cartItems = _storeDb.Carts.Where(cart => cart.CartId == ShoppingCartId);
            foreach (var cartItem in cartItems)
            {
                _storeDb.Carts.Remove(cartItem);
            }
            // Save changes
            _storeDb.SaveChanges();
        }

        public List<Cart> GetCartItems()
        {
            return _storeDb.Carts.Where(cart => cart.CartId == ShoppingCartId).ToList();
        }

        public int GetCount()
        {
            // Get the count of each item in the cart and sum them up
            var count = (from cartItems in _storeDb.Carts
                where cartItems.CartId == ShoppingCartId
                select (int?) cartItems.Count).Sum();
            // Return 0 if all entries are null
            return count ?? 0;
        }

        public decimal GetTotal()
        {
            // Multiply album price by count of that album to get
            // the current price for each of those albums in the cart
            // sum all album price totals to get the cart total
            var total = (from cartItems in _storeDb.Carts
                where cartItems.CartId == ShoppingCartId
                select (int?) cartItems.Count*cartItems.Album.Price).Sum();
            return total ?? decimal.Zero;
        }

        public int CreateOrder(Order order)
        {
            decimal orderTotal = 0;
            var cartItems = GetCartItems();
            // Iterate over the items in the cart, adding the order details for each
            foreach (var item in cartItems)
            {
                var orderDetail = new OrderDetail
                {
                    AlbumId = item.AlbumId,
                    OrderId = order.OrderId,
                    UnitPrice = item.Album.Price,
                    Quantity = item.Count
                };
                // Set the order total of the shopping cart
                orderTotal += (item.Count*item.Album.Price);
                _storeDb.OrderDetails.Add(orderDetail);
            }
            // Set the order's total to the orderTotal count
            order.Total = orderTotal;
            // Save the order
            _storeDb.SaveChanges();
            // Empty the shopping cart
            EmptyCart();
            // Return the OrderId as the confirmation number
            return order.OrderId;
        }

        // We're using HttpContextBase to allow access to cookies.
        public string GetCartId(HttpContextBase context)
        {
            if (context.Session[CartSessionKey] == null)
            {
                if (!string.IsNullOrWhiteSpace(context.User.Identity.Name))
                {
                    context.Session[CartSessionKey] = context.User.Identity.Name;
                }
                else
                {
                    // Generate a new random GUID using System.Guid class
                    var tempCartId = Guid.NewGuid();
                    // Send tempCartId back to client as a cookie
                    context.Session[CartSessionKey] = tempCartId.ToString();
                }
            }
            return context.Session[CartSessionKey].ToString();
        }

        // When a user has logged in, migrate their shopping cart to
        // be associated with their username
        public void MigrateCart(string userName)
        {
            var shoppingCart = _storeDb.Carts.Where(c => c.CartId == ShoppingCartId);
            foreach (var item in shoppingCart)
            {
                item.CartId = userName;
            }
            _storeDb.SaveChanges();
        }
    }
}