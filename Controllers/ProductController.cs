using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductApi.Data;
using ProductApi.Models;
using NewRelic.Api.Agent;

namespace ProductApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly ApiDbContext _context;

    public ProductsController(ApiDbContext context)
    {
        _context = context;
    }

    // GET: api/products (Read All)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
    {
        NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/Products/AllRequests", 1);
        return await _context.Products.ToListAsync();
    }

    // GET: api/products/5 (Read One)
    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) {
            NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/Products/NotFound", 1);
            return NotFound();
        }
        NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/Products/AllRequests", 1);
        return product;
    }

    // POST: api/products (Create)
    [HttpPost]
    public async Task<ActionResult<Product>> PostProduct(Product product)
    {
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/Products/AllRequests", 1);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    // PUT: api/products/5 (Update)
    [HttpPut("{id}")]
    public async Task<IActionResult> PutProduct(int id, Product product)
    {
        if (id != product.Id) return BadRequest();
        _context.Entry(product).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/Products/AllRequests", 1);
        return NoContent();
    }

    // DELETE: api/products/5 (Delete)
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound();
        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/Products/AllRequests", 1);
        return NoContent();
    }
}