import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes } from 'react'

interface FieldShellProps {
  label: string
  error?: string
  children: ReactNode
}

function FieldShell({ label, error, children }: FieldShellProps) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm font-medium text-slate-700">{label}</span>
      {children}
      {error && <span className="mt-1 block text-sm text-red-600">{error}</span>}
    </label>
  )
}

const inputClasses =
  'block w-full rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 disabled:bg-slate-100'

type TextFieldProps = InputHTMLAttributes<HTMLInputElement> & { label: string; error?: string }

export function TextField({ label, error, className = '', ...props }: TextFieldProps) {
  return (
    <FieldShell label={label} error={error}>
      <input className={`${inputClasses} ${className}`} {...props} />
    </FieldShell>
  )
}

type SelectFieldProps = SelectHTMLAttributes<HTMLSelectElement> & { label: string; error?: string; children: ReactNode }

export function SelectField({ label, error, className = '', children, ...props }: SelectFieldProps) {
  return (
    <FieldShell label={label} error={error}>
      <select className={`${inputClasses} bg-white ${className}`} {...props}>
        {children}
      </select>
    </FieldShell>
  )
}
