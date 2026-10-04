// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.querySelectorAll('.quick-chips').forEach(group => {
    const min = document.getElementById(group.dataset.min), max = document.getElementById(group.dataset.max);
    const paint = () => group.querySelectorAll('button').forEach(button => button.classList.toggle('active', button.dataset.min === min.value && button.dataset.max === max.value));
    group.querySelectorAll('button').forEach(button => button.addEventListener('click', () => { min.value = button.dataset.min; max.value = button.dataset.max; paint(); }));
    min?.addEventListener('input', paint); max?.addEventListener('input', paint); paint();
});
